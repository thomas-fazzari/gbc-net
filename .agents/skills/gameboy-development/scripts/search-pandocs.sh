#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 1 ]]; then
  printf 'usage: %s <query> [rg args...]\n' "$(basename "$0")" >&2
  exit 2
fi

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
skill_dir="$(cd -- "$script_dir/.." && pwd)"
docs_dir="$skill_dir/references/pandocs"
query="$1"
shift

rg -n -i --context 2 "$query" "$docs_dir" "$@"
