<h1 align="center">
  GBC.Net
  <br/>
  <a href="https://github.com/thomas-fazzari/gbc-net/actions/workflows/ci.yml"><img src="https://img.shields.io/github/actions/workflow/status/thomas-fazzari/gbc-net/ci.yml?branch=master&style=flat-square&labelColor=11111B&label=CI&logo=githubactions&logoColor=white" alt="CI"></a>
  <a href="https://codecov.io/gh/thomas-fazzari/gbc-net"><img src="https://img.shields.io/codecov/c/github/thomas-fazzari/gbc-net?style=flat-square&labelColor=11111B&label=Coverage&logo=codecov&logoColor=white" alt="Coverage"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-GPL--3.0--only-313244?style=flat-square&labelColor=11111B&logo=gnu&logoColor=white" alt="GPL-3.0-only License"></a>
</h1>

<p align="center">
  A Game Boy, Game Boy Color & Super Game Boy emulator written in C#
</p>

<h2>Getting Started</h2>

<p>Run it from source with:</p>

```sh
make install # Also sets up a linting Git hook
make
```

<h2>Testing</h2>

<p>Integration tests exercise filesystem and database behavior.</p>

```sh
make test          # All tests
make unit          # Unit tests only
make integration   # Integration tests only
```

<h2>Compatibility</h2>

| Target         | Status           | Coverage                                                                                                                                                                                                                                                               | Limitations                                                                                       |
| -------------- | ---------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------- |
| Game Boy       | Functional       | [Mooneye](tests/GbcNet.Tests/Unit/RomTesting/Mooneye/MooneyeAcceptanceRomTests.cs), [Blargg](tests/GbcNet.Tests/Unit/RomTesting/Blargg), [dmg-acid2](tests/GbcNet.Tests/Unit/RomTesting/Visual/DmgAcid2VisualRomTests.cs)                                              | Hardware edge cases may remain                                                                    |
| Game Boy Color | Functional       | [Mooneye CGB](tests/GbcNet.Tests/Unit/RomTesting/Mooneye/MooneyeCgbRomTests.cs), [cgb-acid2](tests/GbcNet.Tests/Unit/RomTesting/Visual/CgbAcid2VisualRomTests.cs)                                                                                                      | Some CGB-specific edge cases remain                                                               |
| Super Game Boy | Functional (HLE) | [SGB boot/model](tests/GbcNet.Tests/Unit/RomTesting/Mooneye/MooneyeSgbRomTests.cs), [SGB commands](tests/GbcNet.Tests/Unit/Sgb/SgbControllerTests.cs)                                                                                                                  | Optional SNES-side commands and SGB2 are unsupported                                              |
| Cartridges     | Partial          | [MBC1](tests/GbcNet.Tests/Unit/Cartridges/Mbc1CartridgeTests.cs), [MBC2](tests/GbcNet.Tests/Unit/Cartridges/Mbc2CartridgeTests.cs), [MBC3](tests/GbcNet.Tests/Unit/Cartridges/Mbc3CartridgeTests.cs), [MBC5](tests/GbcNet.Tests/Unit/Cartridges/Mbc5CartridgeTests.cs) | Uncommon mappers remain unsupported. MBC5 rumble state is emulated without host vibration output. |

<h2>Contributors</h2>

<a href="https://github.com/thomas-fazzari/gbc-net/graphs/contributors">
  <img src="https://contrib.rocks/image?repo=thomas-fazzari/gbc-net" alt="GBC.Net contributors">
</a>
