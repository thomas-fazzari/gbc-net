// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Collections.Immutable;
using System.Globalization;
using Avalonia.Threading;
using GbcNet.Core.Joypad;
using Microsoft.Extensions.Logging;
using SDL;

namespace GbcNet.App.Input;

/// <summary>
/// Owns SDL gamepad handles and translates their canonical controls on the UI thread.
/// </summary>
internal sealed unsafe class GamepadManager(
    InputRouter inputRouter,
    Action togglePause,
    Action toggleFastForward,
    ILogger<GamepadManager> logger
) : IDisposable
{
    private const short PressThreshold = 0x4000;
    private const short ReleaseThreshold = 0x3800;
    private const string UnknownJoystickName = "Unknown joystick";
    private const SDL_InitFlags GamepadSubsystems = (SDL_InitFlags)(
        SDL3.SDL_INIT_GAMEPAD | SDL3.SDL_INIT_EVENTS
    );

    private readonly InputRouter _inputRouter = inputRouter;
    private readonly Action _togglePause = togglePause;
    private readonly Action _toggleFastForward = toggleFastForward;
    private readonly ILogger<GamepadManager> _logger = logger;
    private readonly Dictionary<uint, ConnectedGamepad> _gamepads = [];
    private readonly Dictionary<uint, string> _unsupportedJoysticks = [];
    private DispatcherTimer? _pollTimer;
    private bool _started;
    private bool _disposed;
    private bool _sdlInitialized;
    private int _nextGamepadPosition = 1;

    /// <summary>
    /// Gets whether SDL gamepad input is available after <see cref="Start"/>.
    /// </summary>
    public bool IsAvailable { get; private set; }

    /// <summary>
    /// Gets the SDL/native error that made gamepad input unavailable, if any.
    /// </summary>
    public string? AvailabilityError { get; private set; }

    /// <summary>
    /// Gets immutable snapshots of every opened, recognized gamepad.
    /// </summary>
    public ImmutableArray<GamepadDeviceSnapshot> ConnectedDevices { get; private set; } = [];

    /// <summary>
    /// Gets names of connected joysticks SDL cannot use as gamepads.
    /// </summary>
    public ImmutableArray<string> UnsupportedJoystickNames { get; private set; } = [];

    /// <summary>
    /// Gets the gamepad currently selected by Settings for capture and labels.
    /// </summary>
    public uint? SelectedDeviceId { get; private set; }

    /// <summary>
    /// Gets or sets whether gamepad gameplay input reaches the emulated joypad.
    /// Polling and Settings capture remain active while this is false.
    /// </summary>
    public bool GameplayEnabled { get; private set; } = true;

    /// <summary>
    /// Raised on the UI thread after recognized or unsupported devices change.
    /// </summary>
    public event EventHandler? DevicesChanged;

    /// <summary>
    /// Raised on the UI thread when the selected gamepad presses a portable control.
    /// </summary>
    public event EventHandler<GamepadButtonPressedEventArgs>? AllowedButtonPressed;

    /// <summary>
    /// Initializes the SDL gamepad and event subsystems, opens present gamepads, and starts polling.
    /// </summary>
    public void Start()
    {
        VerifyDispatcherAccess();
        ThrowIfDisposed();

        if (_started)
        {
            return;
        }

        _started = true;

        try
        {
            if (!SDL3.SDL_InitSubSystem(GamepadSubsystems))
            {
                SetUnavailable(GetSdlError());
                return;
            }

            _sdlInitialized = true;
            SDL3.SDL_SetGamepadEventsEnabled(true);
            EnumerateDevices();

            _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(8) };
            _pollTimer.Tick += DrainEvents;
            _pollTimer.Start();
            IsAvailable = true;
            AvailabilityError = null;
        }
        catch (Exception exception)
            when (exception
                    is DllNotFoundException
                        or EntryPointNotFoundException
                        or BadImageFormatException
            )
        {
            SetUnavailable(exception.Message, exception);
            ShutdownSdl();
        }
    }

    /// <summary>
    /// Selects the temporary Settings device. It never affects gameplay routing.
    /// </summary>
    public void SetSelectedDevice(uint? deviceId)
    {
        VerifyDispatcherAccess();
        ThrowIfDisposed();

        SelectedDeviceId = deviceId is { } id && _gamepads.ContainsKey(id) ? id : null;
    }

    /// <summary>
    /// Enables or disables gameplay routing without interrupting SDL polling or capture.
    /// </summary>
    public void SetGameplayEnabled(bool enabled)
    {
        VerifyDispatcherAccess();

        if (_disposed)
        {
            return;
        }

        if (GameplayEnabled == enabled)
        {
            return;
        }

        GameplayEnabled = enabled;

        if (!enabled)
        {
            ReleaseAllContributions();
        }
    }

    /// <summary>
    /// Returns a portable control name augmented with the selected device's physical label when SDL has one.
    /// </summary>
    public string GetButtonDisplayLabel(uint deviceId, GamepadButton button)
    {
        VerifyDispatcherAccess();
        ThrowIfDisposed();

        var canonicalLabel = GetCanonicalButtonLabel(button);

        if (!_gamepads.TryGetValue(deviceId, out var gamepad))
        {
            return canonicalLabel;
        }

        var nativeLabel = SDL3.SDL_GetGamepadButtonLabel(gamepad.Handle, ToSdlButton(button));
        var physicalLabel = GetPhysicalButtonLabel(nativeLabel);

        return physicalLabel is null ? canonicalLabel : $"{canonicalLabel} ({physicalLabel})";
    }

    public void Dispose()
    {
        VerifyDispatcherAccess();

        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopPollTimer();
        ReleaseAllContributions();
        CloseAllGamepads();
        ShutdownSdl();
        IsAvailable = false;
    }

    private void EnumerateDevices()
    {
        using var joystickIds = SDL3.SDL_GetJoysticks();

        if (joystickIds is null)
        {
            return;
        }

        foreach (var joystickId in joystickIds)
        {
            AddOrTrackDevice((uint)joystickId);
        }
    }

    private void DrainEvents(object? sender, EventArgs eventArgs)
    {
        try
        {
            SDL_Event sdlEvent;

            while (SDL3.SDL_PollEvent(&sdlEvent))
            {
                HandleEvent(sdlEvent);
            }
        }
        catch (Exception exception)
            when (exception is DllNotFoundException or EntryPointNotFoundException)
        {
            DisableAfterNativeFailure(exception);
        }
    }

    private void HandleEvent(SDL_Event sdlEvent)
    {
        switch ((SDL_EventType)sdlEvent.type)
        {
            case SDL_EventType.SDL_EVENT_JOYSTICK_ADDED:
            case SDL_EventType.SDL_EVENT_GAMEPAD_ADDED:
                AddOrTrackDevice((uint)sdlEvent.gdevice.which);
                break;

            case SDL_EventType.SDL_EVENT_JOYSTICK_REMOVED:
            case SDL_EventType.SDL_EVENT_GAMEPAD_REMOVED:
                RemoveDevice((uint)sdlEvent.gdevice.which);
                break;

            case SDL_EventType.SDL_EVENT_GAMEPAD_REMAPPED:
                RemapDevice((uint)sdlEvent.gdevice.which);
                break;

            case SDL_EventType.SDL_EVENT_GAMEPAD_BUTTON_DOWN:
                HandleGamepadButton(
                    (uint)sdlEvent.gbutton.which,
                    sdlEvent.gbutton.button,
                    pressed: true
                );
                break;

            case SDL_EventType.SDL_EVENT_GAMEPAD_BUTTON_UP:
                HandleGamepadButton(
                    (uint)sdlEvent.gbutton.which,
                    sdlEvent.gbutton.button,
                    pressed: false
                );
                break;

            case SDL_EventType.SDL_EVENT_GAMEPAD_AXIS_MOTION:
                HandleGamepadAxis(
                    (uint)sdlEvent.gaxis.which,
                    sdlEvent.gaxis.axis,
                    sdlEvent.gaxis.value
                );
                break;
        }
    }

    private void AddOrTrackDevice(uint deviceId)
    {
        var joystickId = (SDL_JoystickID)deviceId;

        if (SDL3.SDL_IsGamepad(joystickId))
        {
            AddGamepad(deviceId, joystickId);
            return;
        }

        AddUnsupportedJoystick(deviceId, joystickId);
    }

    private void AddGamepad(uint deviceId, SDL_JoystickID joystickId)
    {
        if (_gamepads.ContainsKey(deviceId))
        {
            return;
        }

        var handle = SDL3.SDL_OpenGamepad(joystickId);

        if (handle == null)
        {
            GamepadManagerLog.UnableToOpenGamepad(_logger, deviceId, GetSdlError());
            return;
        }

        _gamepads.Add(
            deviceId,
            new ConnectedGamepad(
                handle,
                GetSdlString(SDL3.Unsafe_SDL_GetGamepadNameForID(joystickId)),
                _nextGamepadPosition++
            )
        );
        _unsupportedJoysticks.Remove(deviceId);
        RefreshDeviceSnapshots();
    }

    private void AddUnsupportedJoystick(uint deviceId, SDL_JoystickID joystickId)
    {
        if (_gamepads.ContainsKey(deviceId))
        {
            return;
        }

        var name = GetSdlString(SDL3.Unsafe_SDL_GetJoystickNameForID(joystickId));

        if (
            _unsupportedJoysticks.TryGetValue(deviceId, out var existingName)
            && string.Equals(existingName, name, StringComparison.Ordinal)
        )
        {
            return;
        }

        _unsupportedJoysticks[deviceId] = name;
        RefreshDeviceSnapshots();
    }

    private void RemoveDevice(uint deviceId)
    {
        var changed = _unsupportedJoysticks.Remove(deviceId);

        if (_gamepads.Remove(deviceId, out var gamepad))
        {
            ReleaseDeviceContributions(deviceId);
            SDL3.SDL_CloseGamepad(gamepad.Handle);

            if (SelectedDeviceId == deviceId)
            {
                SelectedDeviceId = null;
            }

            changed = true;
        }

        if (changed)
        {
            RefreshDeviceSnapshots();
        }
    }

    private void RemapDevice(uint deviceId)
    {
        RemoveDevice(deviceId);
        AddOrTrackDevice(deviceId);
    }

    private void HandleGamepadButton(uint deviceId, byte nativeButton, bool pressed)
    {
        if (!_gamepads.TryGetValue(deviceId, out var gamepad))
        {
            return;
        }

        var button = (SDL_GamepadButton)nativeButton;

        if (TryMapButton(button, out var mappedButton))
        {
            if (pressed)
            {
                if (!gamepad.PressedButtons.Add(mappedButton))
                {
                    return;
                }

                if (SelectedDeviceId == deviceId)
                {
                    AllowedButtonPressed?.Invoke(
                        this,
                        new GamepadButtonPressedEventArgs(mappedButton)
                    );
                }
            }
            else if (!gamepad.PressedButtons.Remove(mappedButton))
            {
                return;
            }

            if (GameplayEnabled)
            {
                _inputRouter.ApplyGamepadButton(deviceId, mappedButton, pressed);
            }

            return;
        }

        if (TryMapDirection(button, out var direction))
        {
            UpdateDirection(gamepad, deviceId, direction, pressed, gamepad.DpadDirections);
        }
    }

    private void HandleGamepadAxis(uint deviceId, byte nativeAxis, short value)
    {
        if (!_gamepads.TryGetValue(deviceId, out var gamepad))
        {
            return;
        }

        switch ((SDL_GamepadAxis)nativeAxis)
        {
            case SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFTX:
                gamepad.LeftX = value;
                UpdateStickDirections(gamepad, deviceId);
                break;

            case SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFTY:
                gamepad.LeftY = value;
                UpdateStickDirections(gamepad, deviceId);
                break;

            case SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFT_TRIGGER:
                UpdatePauseTrigger(gamepad, value);
                break;

            case SDL_GamepadAxis.SDL_GAMEPAD_AXIS_RIGHT_TRIGGER:
                UpdateFastForwardTrigger(gamepad, value);
                break;
        }
    }

    private void UpdateStickDirections(ConnectedGamepad gamepad, uint deviceId)
    {
        UpdateDirection(
            gamepad,
            deviceId,
            JoypadButton.Left,
            IsNegativeDirectionActive(
                gamepad.StickDirections.Contains(JoypadButton.Left),
                gamepad.LeftX
            ),
            gamepad.StickDirections
        );
        UpdateDirection(
            gamepad,
            deviceId,
            JoypadButton.Right,
            IsPositiveDirectionActive(
                gamepad.StickDirections.Contains(JoypadButton.Right),
                gamepad.LeftX
            ),
            gamepad.StickDirections
        );
        UpdateDirection(
            gamepad,
            deviceId,
            JoypadButton.Up,
            IsNegativeDirectionActive(
                gamepad.StickDirections.Contains(JoypadButton.Up),
                gamepad.LeftY
            ),
            gamepad.StickDirections
        );
        UpdateDirection(
            gamepad,
            deviceId,
            JoypadButton.Down,
            IsPositiveDirectionActive(
                gamepad.StickDirections.Contains(JoypadButton.Down),
                gamepad.LeftY
            ),
            gamepad.StickDirections
        );
    }

    private void UpdateDirection(
        ConnectedGamepad gamepad,
        uint deviceId,
        JoypadButton direction,
        bool pressed,
        HashSet<JoypadButton> sourceDirections
    )
    {
        if (pressed)
        {
            sourceDirections.Add(direction);
        }
        else
        {
            sourceDirections.Remove(direction);
        }

        var active =
            gamepad.DpadDirections.Contains(direction)
            || gamepad.StickDirections.Contains(direction);
        var changed = active
            ? gamepad.ActiveDirections.Add(direction)
            : gamepad.ActiveDirections.Remove(direction);

        if (changed && GameplayEnabled)
        {
            _inputRouter.ApplyGamepadDirection(deviceId, direction, active);
        }
    }

    private void UpdatePauseTrigger(ConnectedGamepad gamepad, short value)
    {
        var pressed = IsPositiveDirectionActive(gamepad.LeftTriggerHeld, value);

        if (pressed == gamepad.LeftTriggerHeld)
        {
            return;
        }

        gamepad.LeftTriggerHeld = pressed;

        if (pressed && GameplayEnabled)
        {
            _togglePause();
        }
    }

    private void UpdateFastForwardTrigger(ConnectedGamepad gamepad, short value)
    {
        var pressed = IsPositiveDirectionActive(gamepad.RightTriggerHeld, value);

        if (pressed == gamepad.RightTriggerHeld)
        {
            return;
        }

        gamepad.RightTriggerHeld = pressed;

        if (pressed && GameplayEnabled)
        {
            _toggleFastForward();
        }
    }

    private void ReleaseAllContributions()
    {
        foreach (var deviceId in _gamepads.Keys)
        {
            _inputRouter.ReleaseGamepad(deviceId);
        }
    }

    private void ReleaseDeviceContributions(uint deviceId)
    {
        _inputRouter.ReleaseGamepad(deviceId);
    }

    private void RefreshDeviceSnapshots()
    {
        ConnectedDevices =
        [
            .. _gamepads
                .OrderBy(static pair => pair.Value.Position)
                .Select(static pair => new GamepadDeviceSnapshot(
                    pair.Key,
                    pair.Value.Name,
                    CreateDeviceDisplayLabel(pair.Value.Position, pair.Value.Name)
                )),
        ];
        UnsupportedJoystickNames = [.. _unsupportedJoysticks.Values.Order(StringComparer.Ordinal)];
        DevicesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void DisableAfterNativeFailure(Exception exception)
    {
        StopPollTimer();
        ReleaseAllContributions();
        CloseAllGamepads();
        ShutdownSdl();
        SetUnavailable(exception.Message, exception);
    }

    private void CloseAllGamepads()
    {
        foreach (var gamepad in _gamepads.Values)
        {
            SDL3.SDL_CloseGamepad(gamepad.Handle);
        }

        _gamepads.Clear();
        _unsupportedJoysticks.Clear();
        ConnectedDevices = [];
        UnsupportedJoystickNames = [];
        SelectedDeviceId = null;
    }

    private void StopPollTimer()
    {
        if (_pollTimer is not { } pollTimer)
        {
            return;
        }

        pollTimer.Stop();
        pollTimer.Tick -= DrainEvents;
        _pollTimer = null;
    }

    private void ShutdownSdl()
    {
        if (!_sdlInitialized)
        {
            return;
        }

        SDL3.SDL_QuitSubSystem(GamepadSubsystems);
        _sdlInitialized = false;
    }

    private void SetUnavailable(string error, Exception? exception = null)
    {
        IsAvailable = false;
        AvailabilityError = error;

        GamepadManagerLog.GamepadInputUnavailable(_logger, error, exception);
    }

    private static bool TryMapButton(SDL_GamepadButton nativeButton, out GamepadButton button)
    {
        switch (nativeButton)
        {
            case SDL_GamepadButton.SDL_GAMEPAD_BUTTON_SOUTH:
                button = GamepadButton.South;
                return true;
            case SDL_GamepadButton.SDL_GAMEPAD_BUTTON_EAST:
                button = GamepadButton.East;
                return true;
            case SDL_GamepadButton.SDL_GAMEPAD_BUTTON_WEST:
                button = GamepadButton.West;
                return true;
            case SDL_GamepadButton.SDL_GAMEPAD_BUTTON_NORTH:
                button = GamepadButton.North;
                return true;
            case SDL_GamepadButton.SDL_GAMEPAD_BUTTON_BACK:
                button = GamepadButton.Back;
                return true;
            case SDL_GamepadButton.SDL_GAMEPAD_BUTTON_START:
                button = GamepadButton.Start;
                return true;
            case SDL_GamepadButton.SDL_GAMEPAD_BUTTON_LEFT_STICK:
                button = GamepadButton.LeftStick;
                return true;
            case SDL_GamepadButton.SDL_GAMEPAD_BUTTON_RIGHT_STICK:
                button = GamepadButton.RightStick;
                return true;
            case SDL_GamepadButton.SDL_GAMEPAD_BUTTON_LEFT_SHOULDER:
                button = GamepadButton.LeftShoulder;
                return true;
            case SDL_GamepadButton.SDL_GAMEPAD_BUTTON_RIGHT_SHOULDER:
                button = GamepadButton.RightShoulder;
                return true;
            default:
                button = default;
                return false;
        }
    }

    private static bool TryMapDirection(SDL_GamepadButton nativeButton, out JoypadButton direction)
    {
        switch (nativeButton)
        {
            case SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_UP:
                direction = JoypadButton.Up;
                return true;
            case SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_DOWN:
                direction = JoypadButton.Down;
                return true;
            case SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_LEFT:
                direction = JoypadButton.Left;
                return true;
            case SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_RIGHT:
                direction = JoypadButton.Right;
                return true;
            default:
                direction = default;
                return false;
        }
    }

    private static SDL_GamepadButton ToSdlButton(GamepadButton button) =>
        button switch
        {
            GamepadButton.South => SDL_GamepadButton.SDL_GAMEPAD_BUTTON_SOUTH,
            GamepadButton.East => SDL_GamepadButton.SDL_GAMEPAD_BUTTON_EAST,
            GamepadButton.West => SDL_GamepadButton.SDL_GAMEPAD_BUTTON_WEST,
            GamepadButton.North => SDL_GamepadButton.SDL_GAMEPAD_BUTTON_NORTH,
            GamepadButton.Back => SDL_GamepadButton.SDL_GAMEPAD_BUTTON_BACK,
            GamepadButton.Start => SDL_GamepadButton.SDL_GAMEPAD_BUTTON_START,
            GamepadButton.LeftStick => SDL_GamepadButton.SDL_GAMEPAD_BUTTON_LEFT_STICK,
            GamepadButton.RightStick => SDL_GamepadButton.SDL_GAMEPAD_BUTTON_RIGHT_STICK,
            GamepadButton.LeftShoulder => SDL_GamepadButton.SDL_GAMEPAD_BUTTON_LEFT_SHOULDER,
            GamepadButton.RightShoulder => SDL_GamepadButton.SDL_GAMEPAD_BUTTON_RIGHT_SHOULDER,
            _ => throw new ArgumentOutOfRangeException(nameof(button), button, message: null),
        };

    private static string GetCanonicalButtonLabel(GamepadButton button) =>
        button switch
        {
            GamepadButton.South => "South",
            GamepadButton.East => "East",
            GamepadButton.West => "West",
            GamepadButton.North => "North",
            GamepadButton.Back => "Back",
            GamepadButton.Start => "Start",
            GamepadButton.LeftStick => "Left Stick",
            GamepadButton.RightStick => "Right Stick",
            GamepadButton.LeftShoulder => "Left Shoulder",
            GamepadButton.RightShoulder => "Right Shoulder",
            _ => throw new ArgumentOutOfRangeException(nameof(button), button, message: null),
        };

    private static string? GetPhysicalButtonLabel(SDL_GamepadButtonLabel label) =>
        label switch
        {
            SDL_GamepadButtonLabel.SDL_GAMEPAD_BUTTON_LABEL_A => "A",
            SDL_GamepadButtonLabel.SDL_GAMEPAD_BUTTON_LABEL_B => "B",
            SDL_GamepadButtonLabel.SDL_GAMEPAD_BUTTON_LABEL_X => "X",
            SDL_GamepadButtonLabel.SDL_GAMEPAD_BUTTON_LABEL_Y => "Y",
            SDL_GamepadButtonLabel.SDL_GAMEPAD_BUTTON_LABEL_CROSS => "Cross",
            SDL_GamepadButtonLabel.SDL_GAMEPAD_BUTTON_LABEL_CIRCLE => "Circle",
            SDL_GamepadButtonLabel.SDL_GAMEPAD_BUTTON_LABEL_SQUARE => "Square",
            SDL_GamepadButtonLabel.SDL_GAMEPAD_BUTTON_LABEL_TRIANGLE => "Triangle",
            _ => null,
        };

    private static bool IsPositiveDirectionActive(bool active, short value) =>
        active ? value > ReleaseThreshold : value >= PressThreshold;

    private static bool IsNegativeDirectionActive(bool active, short value) =>
        active ? value < -ReleaseThreshold : value <= -PressThreshold;

    private static string CreateDeviceDisplayLabel(int position, string name) =>
        string.Equals(name, UnknownJoystickName, StringComparison.Ordinal)
            ? string.Create(CultureInfo.InvariantCulture, $"Gamepad {position}")
            : string.Create(CultureInfo.InvariantCulture, $"Gamepad {position} ({name})");

    private static string GetSdlString(byte* value) =>
        SDL3.PtrToStringUTF8(value) ?? UnknownJoystickName;

    private static string GetSdlError() =>
        SDL3.PtrToStringUTF8(SDL3.Unsafe_SDL_GetError()) ?? "Unknown SDL error";

    private static void VerifyDispatcherAccess() => Dispatcher.UIThread.VerifyAccess();

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    private sealed class ConnectedGamepad(SDL_Gamepad* handle, string name, int position)
    {
        public SDL_Gamepad* Handle { get; } = handle;

        public string Name { get; } = name;

        public int Position { get; } = position;

        public HashSet<GamepadButton> PressedButtons { get; } = [];

        public HashSet<JoypadButton> ActiveDirections { get; } = [];
        public HashSet<JoypadButton> DpadDirections { get; } = [];

        public HashSet<JoypadButton> StickDirections { get; } = [];

        public short LeftX { get; set; }

        public short LeftY { get; set; }

        public bool LeftTriggerHeld { get; set; }

        public bool RightTriggerHeld { get; set; }
    }
}

internal static partial class GamepadManagerLog
{
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Unable to open SDL gamepad {DeviceId}: {SdlError}"
    )]
    internal static partial void UnableToOpenGamepad(
        ILogger logger,
        uint deviceId,
        string sdlError
    );

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "SDL gamepad input is unavailable: {SdlError}"
    )]
    internal static partial void GamepadInputUnavailable(
        ILogger logger,
        string sdlError,
        Exception? exception
    );
}
