SOLUTION := GbcNet.slnx
APP := src/GbcNet.App/GbcNet.App.csproj
TEST_PROJECT := tests/GbcNet.Tests/GbcNet.Tests.csproj
BENCHMARK_PROJECT := benchmarks/GbcNet.Benchmarks/GbcNet.Benchmarks.csproj
ICON := src/GbcNet.App/Assets/Icon.png
COVERAGE_SETTINGS := $(CURDIR)/tests/coverage.settings.xml
COVERAGE_DIR := $(CURDIR)/artifacts

CONFIGURATION ?= Debug
RUN_CONFIGURATION ?= Release
RUNTIME ?= osx-arm64
TEST_ARGS ?=
BENCHMARK_ARGS ?= --filter '*'

DOTNET ?= dotnet

export DOTNET
