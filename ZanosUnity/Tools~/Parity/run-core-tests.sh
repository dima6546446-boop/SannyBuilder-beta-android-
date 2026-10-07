#!/usr/bin/env bash
# Тесты ядра Unity-версии на Mono без Unity. Требуется mono + mcs.
set -e
cd "$(dirname "$0")"
C=../../Assets/Scripts/Core
mcs -out:/tmp/coretests.exe -nowarn:0168,0219,0649 CoreTests.cs $C/Phys.cs $C/Tire.cs $C/CarSpec.cs $C/CarCatalog.cs $C/Car.cs $C/MapData.cs $C/World.cs $C/Scoring.cs $C/Session.cs $C/Progress.cs
mono /tmp/coretests.exe
