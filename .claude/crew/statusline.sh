#!/usr/bin/env bash
# Claude Code statusLine — shows the LIVE model serving the session.
# Claude Code feeds this command a JSON blob on stdin that includes the ACTUAL model
# ({"model":{"id":"claude-opus-4-8","display_name":"Opus 4.8"}}), so this is the real model
# in use each turn — not a declared frontmatter tier. Pure bash/sed.
#
# No `set -e`: a statusLine must ALWAYS print something. A grep that finds no match exits 1,
# which under -e would abort before the fallback/default ever ran (leaving a blank statusline).

set -uo pipefail
INPUT=$(cat || true)

MODEL=$(printf '%s' "$INPUT" | grep -o '"display_name"[[:space:]]*:[[:space:]]*"[^"]*"' | head -1 | sed 's/.*"\([^"]*\)"$/\1/' || true)
[ -z "$MODEL" ] && MODEL=$(printf '%s' "$INPUT" | grep -o '"id"[[:space:]]*:[[:space:]]*"[^"]*"' | head -1 | sed 's/.*"\([^"]*\)"$/\1/' || true)
[ -z "$MODEL" ] && MODEL="unknown model"

printf '⬢ %s' "$MODEL"
