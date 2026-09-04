// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Tests.Integration;

/// <summary>
/// Provides unique temporary paths that are deleted after each integration test.
/// </summary>
internal static class TestDirectories
{
    /// <summary>
    /// Creates an owner for a unique path under the system temporary directory.
    /// </summary>
    /// <returns>A disposable path owner. The directory is not created eagerly.</returns>
    public static TemporaryDirectory CreateTemporaryDirectory() => new();

    /// <summary>
    /// Owns a unique temporary directory path and deletes the directory on disposal.
    /// </summary>
    public sealed class TemporaryDirectory : IDisposable
    {
        /// <summary>
        /// Gets the unique path. The directory may not exist until the test creates it.
        /// </summary>
        public string Path { get; } =
            System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "gbc-net-tests",
                Guid.NewGuid().ToString("N")
            );

        /// <summary>
        /// Recursively deletes <see cref="Path"/> when it exists.
        /// </summary>
        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
