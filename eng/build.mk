.PHONY: restore build format-check migrations-check lint format test unit integration coverage

restore:
	$(DOTNET) restore $(SOLUTION)
	$(DOTNET) tool restore

build:
	$(DOTNET) build $(SOLUTION) --configuration $(CONFIGURATION)

format-check:
	$(DOTNET) tool run csharpier check .

migrations-check: build
	$(DOTNET) tool run dotnet-ef migrations has-pending-model-changes --project $(APP) --startup-project $(APP) --configuration $(CONFIGURATION) --no-build

lint: format-check build migrations-check

format:
	$(DOTNET) tool run csharpier format .

test: build
	$(DOTNET) test --project $(TEST_PROJECT) --configuration $(CONFIGURATION) --no-build --no-restore -- $(TEST_ARGS)

unit: build
	$(DOTNET) test --project $(TEST_PROJECT) --configuration $(CONFIGURATION) --no-build --no-restore -- --filter-namespace "GbcNet.Tests.Unit*" $(TEST_ARGS)

integration: build
	$(DOTNET) test --project $(TEST_PROJECT) --configuration $(CONFIGURATION) --no-build --no-restore -- --filter-namespace "GbcNet.Tests.Integration*" $(TEST_ARGS)

coverage: build
	mkdir -p "$(COVERAGE_DIR)"
	$(DOTNET) test --project $(TEST_PROJECT) --configuration $(CONFIGURATION) --no-build --no-restore -- --coverage --coverage-output-format cobertura --coverage-output "$(COVERAGE_DIR)/coverage.cobertura.xml" --coverage-settings "$(COVERAGE_SETTINGS)" $(TEST_ARGS)
