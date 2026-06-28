using GbcNet.App.Configuration;
using GbcNet.App.Configuration.Sections.BootRom;

namespace GbcNet.Tests.App.Configuration.Sections.BootRom;

public sealed class BootRomConfigWriterTests
{
    [Fact]
    public void Write_ReplacesExistingBootRomNodeAndKeepsInputNode()
    {
        var tempDirectory = TestDirectories.GetTemporaryDirectoryPath();
        var configPath = Path.Combine(tempDirectory, UserDataPaths.ConfigFileName);

        try
        {
            Directory.CreateDirectory(tempDirectory);
            File.WriteAllText(
                configPath,
                """
                input version=1 {
                  profile "default" {
                    keyboard {
                      bind "a" key="Z"
                    }
                  }
                }

                boot_roms {
                  dmg "old-dmg.bin"
                }
                """
            );

            var result = BootRomConfigWriter.Write(
                configPath,
                new BootRomConfig("new-dmg.bin", "new-cgb.bin", "new-sgb.bin")
            );

            Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Errors));
            var text = File.ReadAllText(configPath);
            Assert.Contains("input version=1", text, StringComparison.Ordinal);
            Assert.DoesNotContain("old-dmg.bin", text, StringComparison.Ordinal);
            Assert.Contains("  dmg \"new-dmg.bin\"", text, StringComparison.Ordinal);
            Assert.Contains("  cgb \"new-cgb.bin\"", text, StringComparison.Ordinal);
            Assert.Contains("  sgb \"new-sgb.bin\"", text, StringComparison.Ordinal);
        }
        finally
        {
            TestDirectories.DeleteDirectoryIfExists(tempDirectory);
        }
    }

    [Fact]
    public void Write_PreservesCommentsOutsideBootRomNode()
    {
        var tempDirectory = TestDirectories.GetTemporaryDirectoryPath();
        var configPath = Path.Combine(tempDirectory, UserDataPaths.ConfigFileName);

        try
        {
            Directory.CreateDirectory(tempDirectory);
            File.WriteAllText(
                configPath,
                """
                // keep top comment
                input version=1 {
                  // keep nested comment
                }

                boot_roms {
                  // old boot rom comment can be replaced
                  dmg "old-dmg.bin"
                }

                // keep trailing comment
                """
            );

            var result = BootRomConfigWriter.Write(
                configPath,
                new BootRomConfig("new-dmg.bin", CgbPath: null)
            );

            Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Errors));
            var text = File.ReadAllText(configPath);
            Assert.Contains("// keep top comment", text, StringComparison.Ordinal);
            Assert.Contains("// keep nested comment", text, StringComparison.Ordinal);
            Assert.Contains("// keep trailing comment", text, StringComparison.Ordinal);
            Assert.DoesNotContain("old-dmg.bin", text, StringComparison.Ordinal);
            Assert.Contains("new-dmg.bin", text, StringComparison.Ordinal);
        }
        finally
        {
            TestDirectories.DeleteDirectoryIfExists(tempDirectory);
        }
    }

    [Fact]
    public void Write_AppendsBootRomNodeWhenMissing()
    {
        var tempDirectory = TestDirectories.GetTemporaryDirectoryPath();
        var configPath = Path.Combine(tempDirectory, UserDataPaths.ConfigFileName);

        try
        {
            Directory.CreateDirectory(tempDirectory);
            File.WriteAllText(configPath, "input version=1 { }" + Environment.NewLine);

            var result = BootRomConfigWriter.Write(configPath, new BootRomConfig(null, "cgb.bin"));

            Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Errors));
            var text = File.ReadAllText(configPath);
            Assert.Contains("boot_roms {", text, StringComparison.Ordinal);
            Assert.Contains("    cgb \"cgb.bin\"", text, StringComparison.Ordinal);
        }
        finally
        {
            TestDirectories.DeleteDirectoryIfExists(tempDirectory);
        }
    }

    [Fact]
    public void Write_ClearsPathsWhenConfigIsEmpty()
    {
        var tempDirectory = TestDirectories.GetTemporaryDirectoryPath();
        var configPath = Path.Combine(tempDirectory, UserDataPaths.ConfigFileName);

        try
        {
            Directory.CreateDirectory(tempDirectory);
            File.WriteAllText(
                configPath,
                """
                input version=1 { }

                boot_roms {
                  dmg "dmg.bin"
                  cgb "cgb.bin"
                  sgb "sgb.bin"
                }
                """
            );

            var result = BootRomConfigWriter.Write(configPath, new BootRomConfig());

            Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Errors));
            var text = File.ReadAllText(configPath);
            Assert.Contains(BootRomConfigSchema.BootRomNodeName, text, StringComparison.Ordinal);
            Assert.DoesNotContain("dmg.bin", text, StringComparison.Ordinal);
            Assert.DoesNotContain("cgb.bin", text, StringComparison.Ordinal);
            Assert.DoesNotContain("sgb.bin", text, StringComparison.Ordinal);
        }
        finally
        {
            TestDirectories.DeleteDirectoryIfExists(tempDirectory);
        }
    }

    [Fact]
    public void Write_EscapesKdlStringPaths()
    {
        var tempDirectory = TestDirectories.GetTemporaryDirectoryPath();
        var configPath = Path.Combine(tempDirectory, UserDataPaths.ConfigFileName);
        const string path = "dir\\boot\"rom.bin";

        try
        {
            Directory.CreateDirectory(tempDirectory);
            File.WriteAllText(configPath, "input version=1 { }" + Environment.NewLine);

            var result = BootRomConfigWriter.Write(
                configPath,
                new BootRomConfig(path, CgbPath: null)
            );

            Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Errors));
            Assert.Contains(
                "dmg \"dir\\\\boot\\\"rom.bin\"",
                File.ReadAllText(configPath),
                StringComparison.Ordinal
            );
        }
        finally
        {
            TestDirectories.DeleteDirectoryIfExists(tempDirectory);
        }
    }

    [Fact]
    public void Write_RejectsDuplicateBootRomSections()
    {
        var tempDirectory = TestDirectories.GetTemporaryDirectoryPath();
        var configPath = Path.Combine(tempDirectory, UserDataPaths.ConfigFileName);

        try
        {
            Directory.CreateDirectory(tempDirectory);
            File.WriteAllText(
                configPath,
                """
                input version=1 { }

                boot_roms {
                  dmg "first.bin"
                }

                boot_roms {
                  cgb "second.bin"
                }
                """
            );

            var result = BootRomConfigWriter.Write(configPath, new BootRomConfig("new-dmg.bin"));

            Assert.True(result.IsFailed);
            Assert.Contains(
                "only one boot_roms node",
                result.Errors[0].Message,
                StringComparison.Ordinal
            );
        }
        finally
        {
            TestDirectories.DeleteDirectoryIfExists(tempDirectory);
        }
    }
}
