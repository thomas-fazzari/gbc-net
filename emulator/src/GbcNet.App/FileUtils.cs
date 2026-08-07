// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.App;

internal static class FileUtils
{
    public static void TryDeleteRegularFile(string path, Action<Exception> onFailure)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            onFailure(exception);
        }
    }
}
