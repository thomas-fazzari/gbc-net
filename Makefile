DOTNET_WORKSPACE := emulator
SOLUTION := GbcNet.slnx
APP := src/GbcNet.App/GbcNet.App.csproj
UNIT_TESTS := tests/GbcNet.Tests.Unit/GbcNet.Tests.Unit.csproj
INTEGRATION_TESTS := tests/GbcNet.Tests.Integration/GbcNet.Tests.Integration.csproj
ICON := src/GbcNet.App/Assets/Icon.png
COVERAGE_SETTINGS := $(CURDIR)/$(DOTNET_WORKSPACE)/tests/coverage.settings.xml
COVERAGE_DIR := $(CURDIR)/artifacts
CONFIGURATION ?= Debug
RUN_CONFIGURATION ?= Release
RUNTIME ?= osx-arm64
TEST_ARGS ?=
CONTAINER_ENGINE ?= $(shell if command -v podman >/dev/null 2>&1; then printf podman; elif command -v docker >/dev/null 2>&1; then printf docker; fi)
DOTNET_SDK_IMAGE ?= mcr.microsoft.com/dotnet/sdk:10.0.302

DOTNET ?= dotnet
BUNX ?= bunx

.DEFAULT_GOAL := run
MAKEFLAGS += --no-builtin-rules --warn-undefined-variables

.PHONY: install setup init run start lint check fix format fmt tests unit integration integration-c coverage bundle package pack icons copyrights contributors

install: ## Restore dependencies and configure Git hooks
	cd "$(DOTNET_WORKSPACE)" && $(DOTNET) restore $(SOLUTION)
	cd "$(DOTNET_WORKSPACE)" && $(DOTNET) tool restore
	git config core.hooksPath .githooks

setup init: install

run:
	cd "$(DOTNET_WORKSPACE)" && $(DOTNET) run --project $(APP) --configuration $(RUN_CONFIGURATION)

start: run

lint:
	$(BUNX) markdownlint-cli2@0.23.1
	cd "$(DOTNET_WORKSPACE)" && $(DOTNET) tool run csharpier check .
	cd "$(DOTNET_WORKSPACE)" && $(DOTNET) build $(SOLUTION) --configuration $(CONFIGURATION)
	cd "$(DOTNET_WORKSPACE)" && $(DOTNET) tool run dotnet-ef migrations has-pending-model-changes --project $(APP) --startup-project $(APP) --configuration $(CONFIGURATION) --no-build

check: lint

format: ## Fix Markdown and C# formatting
	$(BUNX) markdownlint-cli2@0.23.1 --fix
	cd "$(DOTNET_WORKSPACE)" && $(DOTNET) tool run csharpier format .

fix fmt: format

tests: unit integration ## Run all tests

unit: ## Run unit tests
	cd "$(DOTNET_WORKSPACE)" && $(DOTNET) run --project $(UNIT_TESTS) --configuration $(CONFIGURATION) -- $(TEST_ARGS)

integration: ## Run filesystem and database integration tests
	cd "$(DOTNET_WORKSPACE)" && $(DOTNET) run --project $(INTEGRATION_TESTS) --configuration $(CONFIGURATION) -- $(TEST_ARGS)

integration-c: ## Run integration tests in Podman or Docker
	@if [ -z "$(CONTAINER_ENGINE)" ]; then echo "Podman or Docker is required."; exit 1; fi
	$(CONTAINER_ENGINE) run --rm \
		--env DOTNET_CLI_TELEMETRY_OPTOUT=1 \
		--env TESTINGPLATFORM_TELEMETRY_OPTOUT=1 \
		--volume "$(CURDIR):/workspace:ro" \
		--volume gbcnet-nuget:/root/.nuget/packages \
		--workdir /workspace/$(DOTNET_WORKSPACE) \
		$(DOTNET_SDK_IMAGE) \
		dotnet run --project $(INTEGRATION_TESTS) --configuration $(CONFIGURATION) --artifacts-path /tmp/artifacts -- $(TEST_ARGS)

coverage: ## Run all tests and write Cobertura coverage
	mkdir -p "$(COVERAGE_DIR)"
	cd "$(DOTNET_WORKSPACE)" && $(DOTNET) run --project $(UNIT_TESTS) --configuration $(CONFIGURATION) -- --coverage --coverage-output-format cobertura --coverage-output "$(COVERAGE_DIR)/unit.cobertura.xml" --coverage-settings "$(COVERAGE_SETTINGS)" $(TEST_ARGS)
	cd "$(DOTNET_WORKSPACE)" && $(DOTNET) run --project $(INTEGRATION_TESTS) --configuration $(CONFIGURATION) -- --coverage --coverage-output-format cobertura --coverage-output "$(COVERAGE_DIR)/integration.cobertura.xml" --coverage-settings "$(COVERAGE_SETTINGS)" $(TEST_ARGS)


bundle: ## Create platform application bundle
	@case "$(RUNTIME)" in \
		osx-*) packaging/macos/create-app-bundle.sh "$(DOTNET_WORKSPACE)/$(APP)" "$(RUNTIME)" ;; \
		linux-*) packaging/linux/create-app-bundle.sh "$(DOTNET_WORKSPACE)/$(APP)" "$(RUNTIME)" ;; \
		*) echo "Unsupported RUNTIME: $(RUNTIME)" >&2; exit 1 ;; \
	esac

package pack: bundle

icons: ## Generate app icons from Icon.png (GbcNet.App/Assets)
	packaging/generate-icons.sh "$(DOTNET_WORKSPACE)/$(ICON)"

copyrights: ## Update copyright notices
	$(DOTNET) fsi scripts/copyrights.fsx --

contributors: ## List project contributors
	@git shortlog --all --summary | awk '{ sub(/^[[:space:]]*[0-9]+[[:space:]]+/, ""); if (!seen[tolower($$0)]++) contributors[++count] = $$0 } END { printf "Total : %d contributors(s)\n", count; print "----------------------------------------"; for (i = 1; i <= count; i++) print contributors[i] }'
