# Сервер CAUCASUS DRIVE на Cloudflare Workers

Бесплатный игровой сервер (WebSocket + Durable Objects). Инструкция по развёртыванию — в ../../README.md, раздел «Онлайн».

    npm install
    npx wrangler login
    npx wrangler deploy

Проверка локально: `npx wrangler dev --local --port 8799`, затем `cd ../NetTest && dotnet run -c Release ws ws://localhost:8799/ROOM`.
