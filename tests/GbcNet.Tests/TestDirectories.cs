// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Tests;

internal static class TestDirectories
{
    public static string GetTemporaryDirectoryPath() =>
        Path.Combine(Path.GetTempPath(), "gbc-net-tests", Guid.NewGuid().ToString("N"));

    public static void DeleteDirectoryIfExists(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
