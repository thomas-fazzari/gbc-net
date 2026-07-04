#!/usr/bin/env bash
set -eo pipefail

usage() {
        cat <<'USAGE'
Usage: scripts/copyrights.sh [--check]

Adds GPL-3.0-only headers to tracked C# files using per-file git author names.
USAGE
}

check=false
case "${1:-}" in
"")
        ;;
"--check")
        check=true
        ;;
"-h" | "--help")
        usage
        exit 0
        ;;
*)
        usage >&2
        exit 2
        ;;
esac

year="${COPYRIGHT_YEAR:-$(date +%Y)}"
license="GPL-3.0-only"
changed=0

contributors_for_file() {
        git log --follow --reverse --format='%aN' -- "$1" |
                awk 'length($0) && !seen[$0]++ { if (out) out = out ", " $0; else out = $0 } END { print out }'
}

update_file() {
        local file="$1"
        local contributors="$2"
        local tmp

        tmp="$(mktemp)"

        {
                printf '// Copyright (C) %s %s\n' "$year" "$contributors"
                printf '// SPDX-License-Identifier: %s\n\n' "$license"
                awk '
			NR == 1 && /^\/\/ Copyright \(C\) [0-9][0-9][0-9][0-9] / { in_header = 1; next }
			NR == 1 && /^\/\/ SPDX-License-Identifier: / { in_header = 1; next }
			in_header && /^\/\/ SPDX-License-Identifier: / { next }
			in_header && /^$/ { in_header = 0; next }
			{ in_header = 0; print }
		' "$file"
        } >"$tmp"

        if cmp -s "$tmp" "$file"; then
                rm "$tmp"
                return
        fi

        changed=$((changed + 1))
        if "$check"; then
                printf '%s\n' "$file"
                rm "$tmp"
        else
                mv "$tmp" "$file"
        fi
}

while IFS= read -r -d '' file; do
        contributors="$(contributors_for_file "$file")"
        update_file "$file" "${contributors:-thomas-fazzari}"
done < <(git ls-files -z -- '*.cs')

if "$check"; then
        if [ "$changed" -gt 0 ]; then
                printf '%s C# files need license header updates.\n' "$changed" >&2
                exit 1
        fi

        printf 'All C# files have current license headers.\n'
else
        printf 'Updated %s C# files.\n' "$changed"
fi
