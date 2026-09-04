#!/bin/sh

set -uf

usage() {
        printf '%s\n' \
                'Usage: sh eng/scripts/copyrights.sh [--check]' \
                'Adds GPL-3.0-only headers to repository C# files.'
}

if [ "$#" -gt 1 ]; then
        usage >&2
        exit 2
fi

case ${1-} in
'') check=false ;;
--check) check=true ;;
-h | --help)
        usage
        exit 0
        ;;
*)
        usage >&2
        exit 2
        ;;
esac

export LC_ALL=C
year=${COPYRIGHT_YEAR-}
case $year in
'' | *[![:space:]]*) ;;
*) year= ;;
esac

if [ -z "$year" ]; then
        year=$(date +%Y) || {
                printf '%s\n' 'error: failed to determine the current year.' >&2
                exit 1
        }
else
        case $year in
        [0-9][0-9][0-9][0-9]) ;;
        *)
                printf '%s\n' 'error: COPYRIGHT_YEAR must contain exactly four digits.' >&2
                exit 1
                ;;
        esac
fi

CDPATH=
repo_root=$(cd -- "$(dirname -- "$0")/../.." && pwd) || {
        printf '%s\n' 'error: failed to find the repository root.' >&2
        exit 1
}
cd "$repo_root" || exit 1

git_output=$(git ls-files --cached --others --exclude-standard -- '*.cs' 2>&1)
status=$?
if [ "$status" -ne 0 ]; then
        printf 'error: %s\n' "$git_output" >&2
        exit 1
fi

temp_file=$(mktemp "${TMPDIR:-/tmp}/gbcnet-copyrights.XXXXXX") || {
        printf '%s\n' 'error: failed to create a temporary file.' >&2
        exit 1
}
trap 'rm -f "$temp_file"' 0

changed_count=0
changed_files=
failure_count=0

warn_skipped() {
        printf 'warning: skipped %s: %s\n' "$1" "$2" >&2
        failure_count=$((failure_count + 1))
}

transform() {
        awk -v "year=$year" -v "ending=$line_ending" -v "bom=$preserve_bom" '
                BEGIN {
                        ORS = ending == "crlf" ? "\r\n" : "\n"
                        if (bom == "true") printf "\357\273\277"
                        print "// Copyright (C) " year " GBC.Net Contributors"
                        print "// SPDX-License-Identifier: GPL-3.0-only"
                        print ""
                        skipping = 1
                }
                {
                        sub(/\r$/, "")
                        if (skipping && ($0 ~ /^\/\/ SPDX-License-Identifier: / ||
                            $0 ~ /^\/\/ Copyright \(C\) [0-9][0-9][0-9][0-9]( |$)/)) next
                        if (skipping) {
                                skipping = 0
                                if ($0 == "") next
                        }
                        print
                }
        '
}

old_ifs=$IFS
IFS='
'
for file in $git_output; do
        path=./$file
        [ -f "$path" ] || continue
        [ -r "$path" ] || {
                warn_skipped "$file" 'file is not readable'
                continue
        }

        prefix=$(od -An -tx1 -N4 "$path" 2>/dev/null | tr -d '[:space:]')
        preserve_bom=false
        case $prefix in
        efbbbf*) preserve_bom=true ;;
        0000feff* | fffe* | feff*)
                warn_skipped "$file" 'unsupported Unicode byte-order mark'
                continue
                ;;
        esac

        cr=$(printf '\r')
        if grep -q "${cr}$" "$path"; then line_ending=crlf; else line_ending=lf; fi

        if [ "$preserve_bom" = true ]; then
                dd if="$path" bs=1 skip=3 2>/dev/null | transform >"$temp_file"
        else
                transform <"$path" >"$temp_file"
        fi
        status=$?
        [ "$status" -eq 0 ] || {
                warn_skipped "$file" 'failed to generate the updated file'
                continue
        }
        cmp -s "$temp_file" "$path" && continue

        if [ "$check" = true ]; then
                changed_files=${changed_files}${changed_files:+
}$file
                changed_count=$((changed_count + 1))
        elif cp "$temp_file" "$path"; then
                changed_count=$((changed_count + 1))
        else
                warn_skipped "$file" 'failed to write the updated file'
        fi
done
IFS=$old_ifs

if [ "$check" = true ]; then
        if [ "$changed_count" -gt 0 ]; then
                printf '%s\n' "$changed_files"
                printf '%s C# files need license header updates.\n' "$changed_count" >&2
                exit 1
        fi
        [ "$failure_count" -eq 0 ] || exit 1
        printf '%s\n' 'All C# files have current license headers.'
        exit 0
fi

printf 'Updated %s C# files.\n' "$changed_count"
[ "$failure_count" -eq 0 ]
