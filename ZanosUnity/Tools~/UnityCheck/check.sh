#!/usr/bin/env bash
# Проверка компиляции C# без Unity: UnityEngine.*Module.dll (NuGet UnityEngine.Modules 2021.3.33) + заглушка Input System.
# Скачать DLL:  curl -L -o um.nupkg https://api.nuget.org/v3-flatcontainer/unityengine.modules/2021.3.33/unityengine.modules.2021.3.33.nupkg && unzip um.nupkg -d um
set -e
cd "$(dirname "$0")/../.."
U=${UNITY_DLLS:-/tmp/unity-nuget/um/lib/net45}
REFS=""; for m in Core IMGUI Audio ParticleSystem InputLegacy JSONSerialize Physics TextRendering Animation ImageConversion; do REFS="$REFS -r:$U/UnityEngine.${m}Module.dll"; done
echo "== legacy Input Manager"; mcs -target:library -out:/tmp/zanos-check.dll -nowarn:0168,0219,0649,0414 -define:ENABLE_LEGACY_INPUT_MANAGER $REFS Assets/Scripts/Core/*.cs Assets/Scripts/Game/*.cs
echo "== Input System package";  mcs -target:library -out:/tmp/zanos-check2.dll -nowarn:0168,0219,0649,0414 -define:ENABLE_INPUT_SYSTEM $REFS Assets/Scripts/Core/*.cs Assets/Scripts/Game/*.cs "Tools~/UnityCheck/InputSystemStub.cs"
