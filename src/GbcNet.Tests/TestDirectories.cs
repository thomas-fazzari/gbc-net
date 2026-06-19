namespace GbcNet.Tests;

internal static class TestDirectories
{
    public static string CreateTemporaryDirectory() =>
        Path.Combine(Path.GetTempPath(), "gbc-net-tests", Guid.NewGuid().ToString("N"));

    public static void DeleteIfExists(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
