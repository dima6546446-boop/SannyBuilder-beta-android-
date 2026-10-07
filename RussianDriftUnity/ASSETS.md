# ASSETS — внешние ассеты и точки подключения

Проект полностью играбелен на процедурных заменителях. Ниже — что именно можно заменить на настоящие ассеты
(Asset Store или бесплатные CC0), в какие папки класть и как это подхватится. Все источники ниже имеют лицензию CC0 или Standard Asset Store EULA;
проверяйте лицензию перед релизом.

## 1. Модели автомобилей (замена процедурных)

* **Где брать:** Kenney *Car Kit* (CC0, kenney.nl), Quaternius *Ultimate Cars* (CC0), Sketchfab (фильтр CC0 / CC-BY, проверяйте), Asset Store «Low-poly cars».
  Оригинальный дизайн в стиле «классики/девяток» без товарных знаков и точных копий — собственная ответственность.
* **Куда:** `Assets/Art/Cars/<имя>/` (FBX + материалы). Сделайте префаб и назначьте его в поле **`Model Prefab`** соответствующего
  `CarDefinition` (`Assets/ScriptableObjects/Cars/Car_<id>.asset`).
* **Требования к префабу** (адаптер `Vehicle/ExternalCarModel.cs`):
  * метры, нос по **+Z**, начало координат на уровне земли между колёсами;
  * 4 колеса — дочерние трансформы с именами `Wheel_FL`, `Wheel_FR`, `Wheel_RL`, `Wheel_RR`; они вращаются/рулятся/ходят на подвеске;
  * материалы кузова — с «paint» или «body» в имени (перекрашиваются из гаража);
  * по желанию: `Bumper_F`, `Bumper_R`, `Hood`, `Mirror_L/R`, `Spoiler` — отлетают при ударах;
  * подгоните в `CarDefinition` значения `wheelRadius`, `wheelbase`, `width/length/height` под модель. Если адаптер не нашёл 4 колеса, используется процедурная модель.
* Фары/неон/LOD для импортированной модели настраиваются в самом префабе (LODGroup, эмиссия); подсветка фар — реальные Spot-лучи создаются автоматически.

## 2. Звук

Любой клип, положенный в **`Assets/Resources/Audio/<ключ>.wav|ogg`**, заменяет процедурный с тем же ключом (проверка в `AudioManager.Get`).

| Ключ | Назначение |
|---|---|
| `engine_low`, `engine_mid`, `engine_high` | Три **зацикленных** записи двигателя (опорные обороты 1500 / 3500 / 6000 об/мин для 4 цилиндров); нужны **все три** |
| `turbo` | Свист турбины (луп) |
| `blowoff`, `backfire0..2`, `clunk` | Стравливание, выстрелы выхлопа, щелчок коробки |
| `skid`, `wind`, `rain`, `city` | Лупы: визг шин, ветер, дождь, городской фон |
| `impact_l0/l1/h0/h1` | Лёгкие/тяжёлые удары |
| `click`, `confirm`, `back`, `cash`, `levelup`, `beep`, `go`, `checkpoint`, `combo`, `fail` | UI и игровые сигналы |
| `music_menu`, `music_phonk1`, `music_phonk2`, `music_drive`, `music_night` | Музыка (стерео, любой темп) |

Источники: freesound.org (фильтр CC0), Kenney *Audio* паки (CC0), OpenGameArt (проверяйте лицензию), Asset Store «Engine sounds»/«Phonk music».

## 3. Текстуры

PNG/JPG в **`Assets/Resources/Textures/<ключ>.png`** заменяют процедурные текстуры (`ProcTex.Get`). Ключи:
`asphalt`, `concrete`, `facade0..facade3` (кадр = 1 этаж × 2 окна, тайлится 6 м × 3 м), `facade_em` (маска светящихся окон, та же раскладка),
`brick`, `grass`, `dirt`, `metal`, `tire`, `softcircle` (частицы дыма), `glow`, `skid`, `puddle`, `raindrop`, `spark`, `beam`.
Источники: ambientCG, Poly Haven (CC0). Для APK включите ASTC-сжатие в Import Settings (по умолчанию для Android уже ASTC).
HDRI-небо: Poly Haven (CC0) — назначьте `Skybox/Panoramic` в `DayNightCycle.Init` вместо процедурного.

## 4. Окружение

* Модульные дома/заборы/деревья: Kenney *City Kit (Suburban/Commercial/Industrial)*, Quaternius *Ultimate Nature* (CC0).
  Подключение: заменить вызовы `PropBuilder.*` / `CityBuilder.BuildBlock` инстансами префабов (папка `Assets/Art/World/`), оставив коллайдеры.
* Свой серпантин: заменить `SerpentineBuilder` на ваш Terrain/мэш; пути гонки (`WorldInfo.serpentine`, `industrialLoop`, `cityRing`) — это `TrackPath` из контрольных точек.

## 5. Персонаж и анимации

* Процедурный человечек (`World/Humanoid.cs`) уже анимирован кодом. Для настоящего персонажа: Mixamo (бесплатно, Adobe-аккаунт) — модель + клипы Walk / Idle /
  Cheering / Entering Car; Humanoid Rig → `AnimatorController`. Подключение: заменить `Humanoid.Build` на инстанс префаба и переключать
  параметры Animator по `PoseKind` (точки вызова: `SpectatorCrowd`, `DriverCharacter`, гараж `Showroom.SetDriver`).
* Тайм-лайн посадки: клипы в `Timeline` (пакет com.unity.timeline уже подключён) вместо корутины `DriverCharacter.EnterCar`.

## 6. Шрифты и иконки

UI использует встроенный `LegacyRuntime.ttf` (кириллица есть). Для фирменного стиля положите `Font` (например, *Russo One*, *Roboto*, SIL OFL) в `Assets/Art/Fonts`
и назначьте в `Ui.Font`. Иконки сейчас — процедурные спрайты (`UiSprites`).

## 7. SDK монетизации/облака

Реализуйте `IAdService`, `IIapService`, `ICloudSaveService` (см. `Core/Services.cs`) и зарегистрируйте перед `MetaBootstrap.Init`:
`Services.Register<IAdService>(new MyAdMobService());`. Рекомендуемые SDK: Google Mobile Ads (AdMob), Unity IAP, Google Play Games Services.
