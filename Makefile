SOLUTION = GbcNet.slnx
APP = src/GbcNet.App/GbcNet.App.csproj
TESTS = tests/GbcNet.Tests/GbcNet.Tests.csproj
COVERAGE_SETTINGS = $(CURDIR)/tests/GbcNet.Tests/coverage.settings.xml
CONFIGURATION ?= Debug
RUN_CONFIGURATION ?= Release
AOT_RUNTIME ?= osx-arm64

.PHONY: install
install:
	dotnet restore $(SOLUTION)
	dotnet tool restore
	git config core.hooksPath .githooks

.PHONY: lint
lint:
	dotnet tool run csharpier check .
	dotnet tool run slopwatch analyze --fail-on warning
	dotnet build $(SOLUTION) --configuration $(CONFIGURATION)

.PHONY: test
test:
	dotnet test --solution $(SOLUTION) --configuration $(CONFIGURATION)

.PHONY: coverage
coverage:
	dotnet test --project $(TESTS) --configuration $(CONFIGURATION) -- --coverage --coverage-output-format cobertura --coverage-output coverage.cobertura.xml --coverage-settings "$(COVERAGE_SETTINGS)"

.PHONY: aot-check
aot-check:
	dotnet publish $(APP) --configuration Release --runtime $(AOT_RUNTIME) --self-contained true -p:PublishAot=true

.PHONY: app-bundle
app-bundle:
	packaging/package.sh "$(APP)" "$(AOT_RUNTIME)"

.PHONY: run
run:
	dotnet run --project $(APP) --configuration $(RUN_CONFIGURATION)

.PHONY: fix
fix:
	dotnet tool run csharpier format .

.PHONY: copyrights
copyrights:
	dotnet fsi scripts/copyrights.fsx --
