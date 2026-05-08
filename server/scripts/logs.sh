#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
APP_DIR="$(cd "$SCRIPT_DIR/../app" && pwd)"

mkdir -p "$APP_DIR/logs"
tail -n 80 -F "$APP_DIR"/logs/*.log
