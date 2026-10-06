# Headless-тесты

Проверка игры в Chromium без экрана (WebGL через SwiftShader). Тесты гоняют игровой цикл
вручную (`game.tick(1/30)`), поэтому результат не зависит от скорости машины.

```bash
cd caucasus-drive && npm run build && npx vite preview --port 4173 --strictPort &   # сервер
cd tools/test && npm install                                                      # один раз
node game2.mjs /tmp/shots high      # меню, гараж, город, 6 уровней, экзамен; печатает stats и ошибки JS
node logic2.mjs                     # парковка 3★, провал на конусе, экзамен, камеры, красный, дрифт
node foot.mjs /tmp/shots high       # пешком: ходьба, бег, прыжок, корточки, курение, лавка, капот
node crowd2.mjs                     # наезд на прохожего со штрафами и без
node shop.mjs /tmp/shots            # ларьки, покупка, еда
node feat.mjs /tmp/shots            # неон, номера, дождь, радио, прострелы, задания дня
node freedrift.mjs /tmp/shots       # дрифт в свободной езде: шашки в серии, удар, итог, рекорд, близость к припаркованным
node plates.mjs /tmp/shots          # редактор номера в гараже: стрелки, цена, покупка, миграция старого id
node peer-server.cjs &              # локальный брокер PeerJS (для online.mjs)
node online.mjs /tmp/shots          # онлайн: два игрока, быстрая игра, комната по коду, фразы, пешком, выход хоста
node hudsz.mjs /tmp/shots           # наложение кнопок HUD на экранах 640×360…915×412
node gov2.mjs high                  # регулятор качества: модель «упор в GPU», vsync 30, восстановление
node prof.mjs high                  # время CPU на кадр по подсистемам
node interior.mjs /tmp/shots        # салоны: вид из глаз водителя и с пассажирского места
```

Путь к Chromium — переменная `PW_CHROME` (по умолчанию `/opt/pw-browsers/chromium-1194/chrome-linux/chrome`).
Каждый скрипт печатает `NO ERRORS` или список ошибок страницы — это главный критерий.
