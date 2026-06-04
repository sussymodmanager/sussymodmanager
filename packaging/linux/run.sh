#!/bin/sh
# Launch SUSSYMODMANAGER from the folder this script lives in, regardless of CWD.
DIR="$(cd "$(dirname "$0")" && pwd)"
chmod +x "$DIR/SussyModManager" 2>/dev/null
exec "$DIR/SussyModManager" "$@"
