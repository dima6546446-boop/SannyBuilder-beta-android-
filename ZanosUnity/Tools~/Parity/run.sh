#!/usr/bin/env bash
# Сверка физики C# ↔ JS. 1) node ../../../rcd/tools/parity-dump.mjs (эталон)  2) этот скрипт.
set -e
cd "$(dirname "$0")"
mcs -out:/tmp/parity.exe -nowarn:0168,0219 ParityRunner.cs ../../Assets/Scripts/Core/Phys.cs ../../Assets/Scripts/Core/Tire.cs ../../Assets/Scripts/Core/CarSpec.cs ../../Assets/Scripts/Core/CarCatalog.cs ../../Assets/Scripts/Core/Car.cs
mono /tmp/parity.exe expected.csv
