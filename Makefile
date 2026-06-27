SOLUTION = GbcNet.slnx
APP = src/GbcNet.App/GbcNet.App.csproj
CONFIGURATION ?= Debug
RUN_CONFIGURATION ?= Release
AOT_RUNTIME ?= osx-arm64

.PHONY: install
install:
	dotnet restore $(SOLUTION)
	dotnet tool restore

.PHONY: lint
lint:
	dotnet tool run csharpier check .
	dotnet tool run slopwatch analyze --fail-on warning
	dotnet build $(SOLUTION) --configuration $(CONFIGURATION)

.PHONY: test
test:
	dotnet test --solution $(SOLUTION) --configuration $(CONFIGURATION)

.PHONY: aot-check
aot-check:
	dotnet publish $(APP) --configuration Release --runtime $(AOT_RUNTIME) -p:PublishAot=true

.PHONY: app-bundle
app-bundle:
	packaging/package.sh "$(APP)" "$(AOT_RUNTIME)"

.PHONY: run
run:
	dotnet run --project $(APP) --configuration $(RUN_CONFIGURATION)

.PHONY: fix
fix:
	dotnet tool run csharpier format .
