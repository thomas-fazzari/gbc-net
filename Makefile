SOLUTION = GbcNet.slnx
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

.PHONY: fix
fix:
	dotnet tool run csharpier format .
