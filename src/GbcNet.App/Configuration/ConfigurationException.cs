// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.App.Configuration;

internal sealed class ConfigurationException : Exception
{
    public ConfigurationException(string message)
        : base(message) { }

    public ConfigurationException(string message, Exception innerException)
        : base(message: message, innerException: innerException) { }
}
