// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.App.Emulation;

/// <summary>
/// Host allowed pacing multipliers.
/// </summary>
internal enum EmulationSpeed
{
    Normal = 10,

    OnePointFive = 15,

    Two = 20,

    TwoPointFive = 25,

    Three = 30,

    ThreePointFive = 35,

    Four = 40,

    Eight = 80,
}

internal static class EmulationSpeedExtensions
{
    extension(EmulationSpeed speed)
    {
        public EmulationSpeed EnsureDefined(string parameterName)
        {
            if (!Enum.IsDefined(speed))
            {
                throw new ArgumentOutOfRangeException(
                    paramName: parameterName,
                    actualValue: speed,
                    message: "Fast-forward speed must be one of the supported GUI multipliers."
                );
            }

            return speed;
        }

        public string GetDisplayName() =>
            speed switch
            {
                EmulationSpeed.Normal => "1x",
                EmulationSpeed.OnePointFive => "1.5x",
                EmulationSpeed.Two => "2x",
                EmulationSpeed.TwoPointFive => "2.5x",
                EmulationSpeed.Three => "3x",
                EmulationSpeed.ThreePointFive => "3.5x",
                EmulationSpeed.Four => "4x",
                EmulationSpeed.Eight => "8x",
                _ => throw new ArgumentOutOfRangeException(
                    paramName: nameof(speed),
                    actualValue: speed,
                    message: null
                ),
            };
    }
}
