// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Microsoft.Win32.SafeHandles;

namespace GbcNet.App.Shell.Chrome;

[SuppressMessage("Blocker Bug", "S3869:\"SafeHandle.DangerousGetHandle\" should not be called")]
internal sealed partial class MacOsTitleBar : IDisposable
{
    private const string ObjectiveCRuntime = "/usr/lib/libobjc.A.dylib";

    private readonly Window _window;
    private nint _nativeWindow;
    private ToolbarHandle? _toolbar;
    private nint _setTitlebarAppearsTransparent;
    private nint _setTitleVisibility;
    private nint _setToolbar;
    private nint _setToolbarStyle;
    private nint _setToolbarVisible;
    private bool _disposed;

    public MacOsTitleBar(Window window)
    {
        _window = window;

        if (OperatingSystem.IsMacOS())
        {
            _window.Opened += OnOpened;
            _window.PropertyChanged += OnWindowPropertyChanged;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _window.Opened -= OnOpened;
        _window.PropertyChanged -= OnWindowPropertyChanged;

        if (_nativeWindow != 0 && _setToolbar != 0)
        {
            SendToolbarArgument(_nativeWindow, _setToolbar, argument: null);
        }

        _toolbar?.Dispose();
        _toolbar = null;

        GC.SuppressFinalize(this);
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        _window.Opened -= OnOpened;

        _nativeWindow = _window.TryGetPlatformHandle()?.Handle ?? 0;
        if (_nativeWindow == 0)
        {
            return;
        }

        _setToolbar = RegisterSelector("setToolbar:");
        _setToolbarStyle = RegisterSelector("setToolbarStyle:");
        _setToolbarVisible = RegisterSelector("setVisible:");
        _setTitlebarAppearsTransparent = RegisterSelector("setTitlebarAppearsTransparent:");
        _setTitleVisibility = RegisterSelector("setTitleVisibility:");
        _toolbar = CreateToolbar();
        if (_toolbar.IsInvalid)
        {
            _toolbar.Dispose();
            _toolbar = null;
            return;
        }

        ApplyToolbar(_toolbar);
    }

    private static ToolbarHandle CreateToolbar()
    {
        var identifier = CreateNativeString("GbcNetTitleBar");
        if (identifier == 0)
        {
            return new ToolbarHandle();
        }

        var toolbar = Send(
            Send(GetClass("NSToolbar"), RegisterSelector("alloc")),
            RegisterSelector("initWithIdentifier:"),
            identifier
        );
        SendVoid(identifier, RegisterSelector("release"));

        if (toolbar != 0)
        {
            SendBoolean(toolbar, RegisterSelector("setShowsBaselineSeparator:"), value: false);
        }

        return new ToolbarHandle(toolbar);
    }

    private static nint CreateNativeString(string value)
    {
        var utf8 = Marshal.StringToCoTaskMemUTF8(value);
        try
        {
            return Send(
                Send(GetClass("NSString"), RegisterSelector("alloc")),
                RegisterSelector("initWithUTF8String:"),
                utf8
            );
        }
        finally
        {
            Marshal.FreeCoTaskMem(utf8);
        }
    }

    private void ApplyToolbar(ToolbarHandle? toolbar)
    {
        SendInteger(_nativeWindow, _setToolbarStyle, value: 0);
        SendToolbarArgument(_nativeWindow, _setToolbar, toolbar);
        SendBoolean(_nativeWindow, _setTitlebarAppearsTransparent, value: true);
        SendInteger(_nativeWindow, _setTitleVisibility, value: 1);
        SetToolbarVisible(_window.WindowState is not WindowState.FullScreen);
    }

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == Window.WindowStateProperty)
        {
            SetToolbarVisible(_window.WindowState is not WindowState.FullScreen);
        }
    }

    private void SetToolbarVisible(bool isVisible)
    {
        if (_toolbar is { IsInvalid: false })
        {
            SendBoolean(_toolbar.DangerousGetHandle(), _setToolbarVisible, isVisible);
        }
    }

    [LibraryImport(
        ObjectiveCRuntime,
        EntryPoint = "objc_getClass",
        StringMarshalling = StringMarshalling.Utf8
    )]
    private static partial nint GetClass(string name);

    [LibraryImport(
        ObjectiveCRuntime,
        EntryPoint = "sel_registerName",
        StringMarshalling = StringMarshalling.Utf8
    )]
    private static partial nint RegisterSelector(string name);

    [LibraryImport(ObjectiveCRuntime, EntryPoint = "objc_msgSend")]
    private static partial nint Send(nint receiver, nint selector);

    [LibraryImport(ObjectiveCRuntime, EntryPoint = "objc_msgSend")]
    private static partial nint Send(nint receiver, nint selector, nint argument);

    [LibraryImport(ObjectiveCRuntime, EntryPoint = "objc_msgSend")]
    private static partial void SendArgument(nint receiver, nint selector, nint argument);

    [LibraryImport(ObjectiveCRuntime, EntryPoint = "objc_msgSend")]
    private static partial void SendToolbarArgument(
        nint receiver,
        nint selector,
        ToolbarHandle? argument
    );

    [LibraryImport(ObjectiveCRuntime, EntryPoint = "objc_msgSend")]
    private static partial void SendBoolean(
        nint receiver,
        nint selector,
        [MarshalAs(UnmanagedType.I1)] bool value
    );

    [LibraryImport(ObjectiveCRuntime, EntryPoint = "objc_msgSend")]
    private static partial void SendInteger(nint receiver, nint selector, long value);

    [LibraryImport(ObjectiveCRuntime, EntryPoint = "objc_msgSend")]
    private static partial void SendVoid(nint receiver, nint selector);

    private sealed class ToolbarHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public ToolbarHandle()
            : base(ownsHandle: true) { }

        public ToolbarHandle(nint handle)
            : base(ownsHandle: true) => SetHandle(handle);

        protected override bool ReleaseHandle()
        {
            SendVoid(handle, RegisterSelector("release"));
            return true;
        }
    }
}
