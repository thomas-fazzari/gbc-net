.DEFAULT_GOAL := run
MAKEFLAGS += --no-builtin-rules --warn-undefined-variables

include eng/common.mk
include eng/build.mk

.PHONY: install run check bundle icons copyrights

install: restore
	git config core.hooksPath .githooks

run:
	@"$(DOTNET)" run --project "$(APP)" --configuration "$(RUN_CONFIGURATION)"

check: lint test

bundle:
	@case "$(RUNTIME)" in \
		osx-*) sh ./eng/scripts/create-macos-bundle.sh "$(APP)" "$(RUNTIME)" ;; \
		linux-*) sh ./eng/scripts/create-linux-bundle.sh "$(APP)" "$(RUNTIME)" ;; \
		*) echo "Unsupported RUNTIME: $(RUNTIME)" >&2; exit 1 ;; \
	esac

icons:
	@sh ./eng/scripts/generate-icons.sh "$(ICON)"

copyrights:
	@sh ./eng/scripts/copyrights.sh
