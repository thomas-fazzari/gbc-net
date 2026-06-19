using GbcNet.App;
using GbcNet.App.Configuration.Sections.BootRom;

namespace GbcNet.Tests.App.Configuration.Sections.BootRom;

public sealed class BootRomConfigurationWriterTests
{
    [Fact]
    public void Write_ReplacesExistingBootRomNodeAndKeepsInputNode()
    {
        var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var configPath = Path.Combine(tempDirectory, ApplicationDirectoryNames.ConfigFile);

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

            var result = BootRomOptionsWriter.Write(
                configPath,
                new BootRomPathOptions("new-dmg.bin", "new-cgb.bin")
            );

            Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Errors));
            var text = File.ReadAllText(configPath);
            Assert.Contains("input version=1", text, StringComparison.Ordinal);
            Assert.DoesNotContain("old-dmg.bin", text, StringComparison.Ordinal);
            Assert.Contains("  dmg \"new-dmg.bin\"", text, StringComparison.Ordinal);
            Assert.Contains("  cgb \"new-cgb.bin\"", text, StringComparison.Ordinal);
        }
        finally
        {
            TestDirectories.DeleteIfExists(tempDirectory);
        }
    }

    [Fact]
    public void Write_PreservesCommentsOutsideBootRomNode()
    {
        var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var configPath = Path.Combine(tempDirectory, ApplicationDirectoryNames.ConfigFile);

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

            var result = BootRomOptionsWriter.Write(
                configPath,
                new BootRomPathOptions("new-dmg.bin", CgbPath: null)
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
            TestDirectories.DeleteIfExists(tempDirectory);
        }
    }

    [Fact]
    public void Write_AppendsBootRomNodeWhenMissing()
    {
        var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var configPath = Path.Combine(tempDirectory, ApplicationDirectoryNames.ConfigFile);

        try
        {
            Directory.CreateDirectory(tempDirectory);
            File.WriteAllText(configPath, "input version=1 { }" + Environment.NewLine);

            var result = BootRomOptionsWriter.Write(
                configPath,
                new BootRomPathOptions(null, "cgb.bin")
            );

            Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Errors));
            var text = File.ReadAllText(configPath);
            Assert.Contains("boot_roms {", text, StringComparison.Ordinal);
            Assert.Contains("    cgb \"cgb.bin\"", text, StringComparison.Ordinal);
        }
        finally
        {
            TestDirectories.DeleteIfExists(tempDirectory);
        }
    }

    [Fact]
    public void Write_ClearsPathsWhenOptionsAreEmpty()
    {
        var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var configPath = Path.Combine(tempDirectory, ApplicationDirectoryNames.ConfigFile);

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
                }
                """
            );

            var result = BootRomOptionsWriter.Write(configPath, new BootRomPathOptions());

            Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Errors));
            var text = File.ReadAllText(configPath);
            Assert.Contains(BootRomOptionsSchema.BootRomNodeName, text, StringComparison.Ordinal);
            Assert.DoesNotContain("dmg.bin", text, StringComparison.Ordinal);
            Assert.DoesNotContain("cgb.bin", text, StringComparison.Ordinal);
        }
        finally
        {
            TestDirectories.DeleteIfExists(tempDirectory);
        }
    }

    [Fact]
    public void Write_EscapesKdlStringPaths()
    {
        var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var configPath = Path.Combine(tempDirectory, ApplicationDirectoryNames.ConfigFile);
        const string path = "dir\\boot\"rom.bin";

        try
        {
            Directory.CreateDirectory(tempDirectory);
            File.WriteAllText(configPath, "input version=1 { }" + Environment.NewLine);

            var result = BootRomOptionsWriter.Write(
                configPath,
                new BootRomPathOptions(path, CgbPath: null)
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
            TestDirectories.DeleteIfExists(tempDirectory);
        }
    }

    [Fact]
    public void Write_RejectsDuplicateBootRomSections()
    {
        var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var configPath = Path.Combine(tempDirectory, ApplicationDirectoryNames.ConfigFile);

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

            var result = BootRomOptionsWriter.Write(
                configPath,
                new BootRomPathOptions("new-dmg.bin", null)
            );

            Assert.True(result.IsFailed);
            Assert.Contains(
                "only one boot_roms node",
                result.Errors[0].Message,
                StringComparison.Ordinal
            );
        }
        finally
        {
            TestDirectories.DeleteIfExists(tempDirectory);
        }
    }
}
