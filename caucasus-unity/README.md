# CAUCASUS DRIVE — версия на Unity

Порт игры на **Unity 6 + URP** для Android. Всё (город, машины, интерфейс, звук) собирается
**кодом при запуске**, сцена пустая — поэтому проект лёгкий и его нельзя «сломать» в редакторе.

## Что внутри

| Часть | Что сделано |
|---|---|
| Машины | 10 моделей АвтоВАЗ + ДПС и такси из Blender. Для игрока **LOD_ultra** (≈80–90 тыс. треугольников, гладкий кузов, качество «Высокое») и LOD_hi (≈25–33 тыс.); для трафика LOD0/LOD1 с одним материалом-палитрой (1 draw call на машину) |
| Физика | Дословный порт веб-версии: шины, перенос веса, автомат R/N/D и механика, дрифт (перегазовка, ручник, помощник контрруления). Столкновения — PhysX. Проверено: при одинаковом вводе Unity-версия даёт те же цифры, что и веб |
| Город | Автозаводский район 6×6 кварталов: дороги с разметкой, зебры, панельки 5 и 9 этажей, башни, гаражи, парк, АЗС, деревья, фонари, светофоры, забор, автодром ДОСААФ, билборды с нашим Telegram. Окна светятся ночью |
| Трафик | ИИ: светофоры, повороты, перестроения, уступает при левом повороте, объезжает стоящих, сигналит |
| Режимы | **Парковка** — 30 уровней со звёздами (те же, что в веб-версии). **Свободная езда** — дрифт-очки, «шашки», рубли, заправка на АЗС |
| Гараж | 3D-гараж: покупка машин, покраска, диски, подвеска, тонировка, мотор, шины, неон, номера |
| Интерфейс | Руль, стрелки или наклон, газ, тормоз, ручник, R·N·D (или + / −), гудок, поворотники, фары, камеры, мини-карта, стрелка навигации |
| Звук | Процедурный: мотор по оборотам и нагрузке, визг шин, гудок, эффекты |
| Оптимизация | URP, кварталы склеены в общие меши, LOD у трафика, все огни города — одна система частиц (1 draw call), три пресета качества + автоподстройка разрешения и теней под FPS |

**Ещё не перенесено:** прогулки пешком, прохожие, ларьки, радио, дождь, задания дня, салон
с видом из глаз, экзамен ГИБДД, дрифт-зона, онлайн. Будут следующими этапами.

---

## Как собрать APK на компьютере (рекомендуется)

Нужен компьютер с Windows, macOS или Linux, около 15 ГБ места и интернет.

1. **Установи Unity Hub**: https://unity.com/download. Войди в аккаунт Unity — лицензия Personal бесплатная.
2. В Unity Hub: **Installs → Install Editor →** выбери **Unity 6 (6000.0 LTS)**. В списке модулей отметь
   **Android Build Support** вместе с вложенными **OpenJDK** и **Android SDK & NDK Tools**. Жди установку.
3. **Projects → New project →** шаблон **«Universal 3D»** (это URP) → имя `CaucasusDrive` → **Create project**.
4. Закрой Unity. Из архива скопируй папку `Assets/CaucasusDrive` в папку `Assets` нового проекта
   (рядом с его `Scenes` и `Settings`).
5. Открой проект. Подожди, пока Unity всё импортирует и скомпилирует (полоска внизу справа).
   Если спросит про Input System — жми **Yes**.
6. В верхнем меню появится **CAUCASUS DRIVE → 1. Настроить проект**. Нажми: он выставит Android,
   IL2CPP, ARM64, горизонтальный экран, создаст материалы и сцену.
7. Можно нажать **▶ Play** и поиграть прямо в редакторе: WASD — езда, пробел — ручник, H — гудок,
   C — камера, Esc — пауза.
8. **CAUCASUS DRIVE → 2. Собрать APK**. Первая сборка идёт 5–15 минут. Готовый файл —
   `Builds/CaucasusDrive.apk` в папке проекта (откроется сам). Перекинь его на телефон и установи.

Другой путь: открыть в Unity Hub всю папку `caucasus-unity` (**Add → Add project from disk**). Пункт
меню «Настроить проект» сам создаст настройки URP.

## Сборка APK без компьютера (через GitHub)

В репозитории есть workflow `.github/workflows/caucasus-unity-apk.yml` (game-ci). Он соберёт APK
и выложит его в **Releases → «CAUCASUS DRIVE (Unity)»**. Unity требует лицензию, поэтому один раз
нужен компьютер:

1. Установи Unity Hub и войди. Файл лицензии `Unity_lic.ulf` появится здесь:
   - Windows: `C:\ProgramData\Unity\Unity_lic.ulf`
   - macOS: `/Library/Application Support/Unity/Unity_lic.ulf`
   - Linux: `~/.local/share/unity3d/Unity/Unity_lic.ulf`
2. В репозитории: **Settings → Secrets and variables → Actions → New repository secret**:
   - `UNITY_LICENSE` — всё содержимое файла `.ulf`;
   - `UNITY_EMAIL` — почта аккаунта Unity;
   - `UNITY_PASSWORD` — пароль аккаунта Unity.
3. **Actions → CAUCASUS DRIVE Unity APK → Run workflow**. Сборка идёт 30–60 минут.

Без секретов workflow просто пропускается.

## Если что-то не так

- **Розовые объекты.** Не нажат «1. Настроить проект», или проект создан не из шаблона Universal 3D.
  Нажми пункт меню ещё раз.
- **Ошибки про `UnityEngine.InputSystem`.** **Edit → Project Settings → Player → Active Input
  Handling → Both**, перезапусти Unity.
- **Сборка ругается на Android SDK.** В Unity Hub у установленной версии: **⚙ → Add modules →
  Android Build Support** (с OpenJDK и SDK/NDK).
- **Низкий FPS.** В игре: Настройки → Графика → «Среднее» или «Низкое». Регулятор и так снижает
  разрешение, если FPS падает ниже 48.

## Структура

```
Assets/CaucasusDrive/
  Scripts/Core      — App (запуск и цикл), SaveData, Quality (+ день/ночь), CameraRig, GameAudio, In (ввод), Mats (материалы и текстуры)
  Scripts/Vehicles  — VehiclePhysics (порт), PlayerCar (мост физика ↔ PhysX), CarVisual, CarMeshLibrary, CarDefs, SkidMarks
  Scripts/World     — City (генератор города), RoadGraph (+ светофоры), MeshBuilder, Glow
  Scripts/Traffic   — Traffic (ИИ трафика и пул машин)
  Scripts/Gameplay  — Levels (30 уровней), Modes (парковка, свободная езда), Drift (судейство)
  Scripts/UI        — UIKit, HUD (+ сенсорное управление), Menus (+ 3D-гараж)
  Editor            — CaucasusSetup (меню «CAUCASUS DRIVE»: настройка и сборка APK)
  Resources/Cars    — модели машин (.bytes, gzip), экспорт: caucasus-drive/tools/blender/export_unity.py
```

Пересобрать модели после правок в Blender:
`blender -b --python caucasus-drive/tools/blender/export_unity.py -- caucasus-unity/Assets/CaucasusDrive/Resources/Cars`
(файлы сразу сжимаются gzip).
