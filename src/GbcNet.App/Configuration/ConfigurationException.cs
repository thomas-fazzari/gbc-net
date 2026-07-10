// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.App.Configuration;

internal sealed class ConfigurationException : Exception
{
    public ConfigurationException() { }

    public ConfigurationException(string message)
        : base(message) { }

    public ConfigurationException(string message, Exception innerException)
        : base(message, innerException) { }
}
