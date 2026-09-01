#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 1 ]]; then
  printf 'usage: %s <chapter-slug-or-path>\n' "$(basename "$0")" >&2
  exit 2
fi

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
skill_dir="$(cd -- "$script_dir/.." && pwd)"
chapters_dir="$skill_dir/references/pandocs/chapters"
name="$1"

case "$name" in
  *.md) file="$chapters_dir/$(basename "$name")" ;;
  *) file="$chapters_dir/${name}.md" ;;
esac

if [[ ! -f "$file" ]]; then
  printf 'chapter not found: %s\n\nAvailable chapters:\n' "$name" >&2
  find "$chapters_dir" -maxdepth 1 -type f -name '*.md' -print | sed 's#.*/##; s#\\.md$##' | sort >&2
  exit 1
fi

sed -n '1,240p' "$file"
