#!/bin/bash
# Wrapper para que Claude envíe tareas a Kilo (GLM 5.1)
#
# Uso:
#   ./kilowrapper.sh "prompt"
#   ./kilowrapper.sh "prompt" path/al/spec.md
#   ./kilowrapper.sh "prompt" --auto        # auto-aprueba permisos (desatendido)

PROMPT="${1:-}"
SPEC_FILE="${2:-}"
AUTO_FLAG=""

# Detectar flag --auto en cualquier posición
for arg in "$@"; do
  if [ "$arg" = "--auto" ]; then
    AUTO_FLAG="--auto"
  fi
done

# Si hay spec file, leerla y agregar al prompt
if [ -n "$SPEC_FILE" ] && [ -f "$SPEC_FILE" ]; then
  SPEC_CONTENT=$(cat "$SPEC_FILE")
  PROMPT="$PROMPT

=== ESPECIFICACIÓN ($SPEC_FILE) ===
$SPEC_CONTENT"
fi

exec kilo run "$PROMPT" --model zai/glm-5.1 $AUTO_FLAG
