#!/bin/bash
# Wrapper para que Claude envíe tareas a Kimi k2.6
#
# Uso:
#   ./kimiwrapper.sh "prompt"
#   ./kimiwrapper.sh "prompt" path/al/spec.md
#   ./kimiwrapper.sh "prompt" --plan     # activa plan mode antes de ejecutar

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROMPT="${1:-}"
SPEC_FILE="${2:-}"
EXTRA_FLAGS=""

for arg in "$@"; do
  if [ "$arg" = "--plan" ]; then
    EXTRA_FLAGS="$EXTRA_FLAGS --plan"
  fi
done

if [ -n "$SPEC_FILE" ] && [ -f "$SPEC_FILE" ]; then
  SPEC_CONTENT=$(cat "$SPEC_FILE")
  PROMPT="$PROMPT

=== ESPECIFICACIÓN ($SPEC_FILE) ===
$SPEC_CONTENT"
fi

exec kimi-cli --print \
  --work-dir "$SCRIPT_DIR" \
  --prompt "$PROMPT" \
  $EXTRA_FLAGS
