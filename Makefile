SOLUTION = GbcNet.slnx
APP = src/GbcNet.Gui/GbcNet.Gui.csproj
CONFIGURATION ?= Debug

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
	dotnet test $(SOLUTION) --configuration $(CONFIGURATION)

.PHONY: run
run:
	dotnet run --project $(APP) --configuration $(CONFIGURATION)

.PHONY: fix
fix:
	dotnet tool run csharpier format .
