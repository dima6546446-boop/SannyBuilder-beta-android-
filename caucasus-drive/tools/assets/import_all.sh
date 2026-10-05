#!/usr/bin/env bash
# Скачать модели машин с Sketchfab (CC-BY, список — assets.json) и встроить их в обе версии игры.
#   SKETCHFAB_TOKEN=… tools/assets/import_all.sh [id ...]
# Токен: sketchfab.com → Settings → Password & API → API Token (бесплатный аккаунт).
# Шаги на каждую машину: скачать GLB → Blender (tools/blender/import_asset.py: ориентация, масштаб по
# колёсной базе, удаление колёс, материалы игры, номера, 4 LOD) → public/models/<id>.glb и
# caucasus-unity/.../Cars/<id>.bytes → подгонка колеи и радиуса колёс в cars.js и CarDefs.cs → титры.
set -euo pipefail
cd "$(dirname "$0")/../.."                       # caucasus-drive/
: "${SKETCHFAB_TOKEN:?Нужна переменная SKETCHFAB_TOKEN (API-токен Sketchfab)}"
BLENDER="${BLENDER:-$(command -v blender || ls -d /tmp/claude-*/*/*/scratchpad/blender-4.2*/blender 2>/dev/null | head -1)}"
[ -x "$BLENDER" ] || { echo "Не найден Blender 4.2 (переменная BLENDER)"; exit 1; }
RAW=tools/assets/raw; REP=tools/assets/reports; mkdir -p "$RAW" "$REP"
UNITY=../caucasus-unity/Assets/CaucasusDrive/Resources/Cars
IDS=("$@")
[ ${#IDS[@]} -eq 0 ] && mapfile -t IDS < <(node -e "for (const c of require('./tools/assets/assets.json').cars) console.log(c.id)")

for id in "${IDS[@]}"; do
  read -r uid flip < <(node -e "const c=require('./tools/assets/assets.json').cars.find(c=>c.id==='$id'); console.log(c.uid, c.flip?'--flip':'')")
  if [ ! -s "$RAW/$id.glb" ]; then
    echo "== $id: скачиваю $uid"
    json=$(curl -fsS -H "Authorization: Token $SKETCHFAB_TOKEN" "https://api.sketchfab.com/v3/models/$uid/download")
    url=$(node -e "const j=JSON.parse(process.argv[1]); console.log((j.glb||{}).url||'')" "$json")
    if [ -n "$url" ]; then curl -fsS -o "$RAW/$id.glb" "$url"
    else
      zurl=$(node -e "const j=JSON.parse(process.argv[1]); console.log((j.gltf||{}).url||'')" "$json")
      [ -n "$zurl" ] || { echo "   нет GLB/glTF для $id"; continue; }
      curl -fsS -o "$RAW/$id.zip" "$zurl"; rm -rf "$RAW/$id"; mkdir -p "$RAW/$id"; unzip -q -o "$RAW/$id.zip" -d "$RAW/$id"
      ln -sf "$(cd "$RAW/$id" && pwd)/$(cd "$RAW/$id" && ls *.gltf | head -1)" "$RAW/$id.glb"
    fi
  fi
  echo "== $id: импорт в Blender"
  "$BLENDER" -b --python tools/blender/import_asset.py -- "$id" "$RAW/$id.glb" public/models "$UNITY" --report "$REP/$id.json" $flip \
    2>&1 | grep -E "IMPORT_REPORT|Error|Traceback" | cut -c1-400
done
node tools/assets/apply_fit.mjs
echo "Готово. Проверь превью: blender -b --python tools/blender/preview.py -- /tmp/p.png vaz2107,niva front34 public/models"
