#!/usr/bin/env bash
# Запуск игры одной командой: ./run.sh  (установит зависимости при первом запуске)
set -e
cd "$(dirname "$0")"
[ -d node_modules ] || npm install
npm start
