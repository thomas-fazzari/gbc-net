SOLUTION = GbcNet.slnx
APP = src/GbcNet.App/GbcNet.App.csproj
TESTS = tests/GbcNet.Tests/GbcNet.Tests.csproj
COVERAGE_SETTINGS = $(CURDIR)/tests/GbcNet.Tests/coverage.settings.xml
CONFIGURATION ?= Debug
RUN_CONFIGURATION ?= Release
RUNTIME ?= osx-arm64

.PHONY: install
install:
	dotnet restore $(SOLUTION)
	dotnet tool restore
	git config core.hooksPath .githooks

.PHONY: run
run:
	dotnet run --project $(APP) --configuration $(RUN_CONFIGURATION)

.PHONY: lint
lint:
	dotnet tool run csharpier check .
	dotnet tool run slopwatch analyze --fail-on warning
	dotnet build $(SOLUTION) --configuration $(CONFIGURATION)

.PHONY: fix
fix:
	dotnet tool run csharpier format .

.PHONY: test
test:
	dotnet test --solution $(SOLUTION) --configuration $(CONFIGURATION)

.PHONY: coverage
coverage:
	dotnet test --project $(TESTS) --configuration $(CONFIGURATION) -- --coverage --coverage-output-format cobertura --coverage-output coverage.cobertura.xml --coverage-settings "$(COVERAGE_SETTINGS)"

.PHONY: mem-index
mem-index:
	codebase-memory-mcp cli index_repository --repo-path "$(CURDIR)"

.PHONY: bundle
bundle:
	@case "$(RUNTIME)" in \
		osx-*) packaging/macos/create-app-bundle.sh "$(APP)" "$(RUNTIME)" ;; \
		linux-*) packaging/linux/create-app-bundle.sh "$(APP)" "$(RUNTIME)" ;; \
		*) echo "Unsupported RUNTIME: $(RUNTIME)" >&2; exit 1 ;; \
	esac

.PHONY: copyrights
copyrights:
	dotnet fsi scripts/copyrights.fsx --
