#!/bin/bash
# Run AutoPlayer headless with CEF disabled (CEF blocks headless frame loop)
# Usage: bash run_autoplay.sh [strategy] [max_waves]

GODOT="/c/Program Files (x86)/Godot_v4.6.1-stable_mono_win64/Godot_v4.6.1-stable_mono_win64_console.exe"
PROJECT_DIR="$(cd "$(dirname "$0")" && pwd)"
CEF_EXT="$PROJECT_DIR/addons/godot_cef/godot_cef.gdextension"
CEF_BAK="$PROJECT_DIR/addons/godot_cef/godot_cef.gdextension.disabled"

# Disable CEF
if [ -f "$CEF_EXT" ]; then
    mv "$CEF_EXT" "$CEF_BAK"
    echo "[run_autoplay] CEF disabled for headless run"
fi

# Run
"$GODOT" --headless --path "$PROJECT_DIR" -- --autoplay "$@"
EXIT_CODE=$?

# Re-enable CEF
if [ -f "$CEF_BAK" ]; then
    mv "$CEF_BAK" "$CEF_EXT"
    echo "[run_autoplay] CEF re-enabled"
fi

exit $EXIT_CODE
