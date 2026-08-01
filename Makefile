SOLUTION := GbcNet.slnx
APP := src/GbcNet.App/GbcNet.App.csproj
TESTS := tests/GbcNet.Tests/GbcNet.Tests.csproj
ICON := src/GbcNet.App/Assets/Icon.png
COVERAGE_SETTINGS := $(CURDIR)/tests/GbcNet.Tests/coverage.settings.xml
CONFIGURATION ?= Debug
RUN_CONFIGURATION ?= Release
RUNTIME ?= osx-arm64

DOTNET ?= dotnet
NPX ?= npx
CODEBASE_MEMORY ?= codebase-memory-mcp

.DEFAULT_GOAL := run
MAKEFLAGS += --no-builtin-rules --warn-undefined-variables

.PHONY: all help install setup init run start lint check fix format fmt test tests coverage mem-index index bundle package pack icons copyrights contributors

all: lint test

help:
	@awk 'BEGIN { FS = ":.*##"; print "Usage: make <target> [VARIABLE=value]\n\nTargets:" } /^[[:alnum:]_.-]+:.*##/ { printf "  %-14s %s\n", $$1, $$2 }' $(MAKEFILE_LIST)

install: ## Restore dependencies and configure Git hooks
	$(DOTNET) restore $(SOLUTION)
	$(DOTNET) tool restore
	git config core.hooksPath .githooks

setup init: install

run:
	$(DOTNET) run --project $(APP) --configuration $(RUN_CONFIGURATION)

start: run

lint:
	$(NPX) --yes markdownlint-cli2@0.23.1
	$(DOTNET) tool run csharpier check .
	$(DOTNET) tool run slopwatch analyze --fail-on warning
	$(DOTNET) build $(SOLUTION) --configuration $(CONFIGURATION)

check: lint

format: ## Fix Markdown and C# formatting
	$(NPX) --yes markdownlint-cli2@0.23.1 --fix
	$(DOTNET) tool run csharpier format .

fix fmt: format

test: ## Run test suite
	$(DOTNET) test --solution $(SOLUTION) --configuration $(CONFIGURATION)

tests: test

coverage: ## Run tests and write Cobertura coverage
	$(DOTNET) test --project $(TESTS) --configuration $(CONFIGURATION) -- --coverage --coverage-output-format cobertura --coverage-output coverage.cobertura.xml --coverage-settings "$(COVERAGE_SETTINGS)"

mem-index: ## Index repository for Codebase Memory
	$(CODEBASE_MEMORY) cli index_repository --repo-path "$(CURDIR)"

index: mem-index

bundle: ## Create platform application bundle
	@case "$(RUNTIME)" in \
		osx-*) packaging/macos/create-app-bundle.sh "$(APP)" "$(RUNTIME)" ;; \
		linux-*) packaging/linux/create-app-bundle.sh "$(APP)" "$(RUNTIME)" ;; \
		*) echo "Unsupported RUNTIME: $(RUNTIME)" >&2; exit 1 ;; \
	esac

package pack: bundle

icons: ## Generate app icons from Icon.png (GbcNet.App/Assets)
	packaging/generate-icons.sh "$(ICON)"

copyrights: ## Update copyright notices
	$(DOTNET) fsi scripts/copyrights.fsx --

contributors: ## List project contributors
	@git shortlog --all --summary | awk '{ sub(/^[[:space:]]*[0-9]+[[:space:]]+/, ""); if (!seen[tolower($$0)]++) contributors[++count] = $$0 } END { printf "Total : %d contributors(s)\n", count; print "----------------------------------------"; for (i = 1; i <= count; i++) print contributors[i] }'
