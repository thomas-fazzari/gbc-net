SOLUTION = GbcNet.slnx
APP = src/GbcNet.Gui/GbcNet.Gui.csproj
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
	dotnet build $(SOLUTION) --configuration $(CONFIGURATION)

.PHONY: test
test:
	dotnet test --solution $(SOLUTION) --configuration $(CONFIGURATION)

.PHONY: aot-check
aot-check:
	dotnet publish $(APP) --configuration Release --runtime $(AOT_RUNTIME) -p:PublishAot=true

.PHONY: run
run:
	dotnet run --project $(APP) --configuration $(RUN_CONFIGURATION)

.PHONY: fix
fix:
	dotnet tool run csharpier format .
