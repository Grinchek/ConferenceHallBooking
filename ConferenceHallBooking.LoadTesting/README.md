# Навантажувальне тестування (dev)

Консольний клієнт, який емулює одночасну роботу багатьох клієнтів Web API: GET / POST / PUT, асинхронно, з обмеженням паралельності через `SemaphoreSlim` та збором статистики.

## Передумови

1. Запущений API (Development), наприклад:

```bash
cd src/ConferenceHallBooking.Api
dotnet run --launch-profile http
```

За замовчуванням API слухає `http://localhost:5105`.  
API Key (dev): `dev-api-key-change-me` (заголовок `X-Api-Key`).

2. Переконайтесь, що в базі є seed-зали (створюються при старті API).

## Запуск тестера

```bash
cd ConferenceHallBooking.LoadTesting
dotnet run -- [опції]
```

Усе після `--` передається в додаток.

### Параметри

| Параметр | Default | Опис |
|----------|---------|------|
| `--base-url` | `http://localhost:5105` | Базова адреса API |
| `--api-key` | `dev-api-key-change-me` | Значення `X-Api-Key` |
| `--tasks` | `1000` | Кількість асинхронних задач за один прогін |
| `--concurrency` | `10` | Максимум одночасних HTTP-запитів (`SemaphoreSlim`) |
| `--scenarios` | — | Кілька рівнів concurrency через кому, напр. `10,50,100` |
| `--help` | — | Довідка |

Якщо задано `--scenarios`, значення `--concurrency` для прогонів не використовується — ганяються всі перелічені рівні підряд.

### Як працює паралельність

Створюється N асинхронних задач (`Task.WhenAll`). Перед кожним HTTP-запитом задача бере слот у `SemaphoreSlim(concurrency)`, після відповіді — звільняє. Так одночасно до сервера йде не більше заданої кількості запитів.

## Приклади (відповідають ТЗ)

Мінімум: 1000 задач / 10 concurrent:

```bash
dotnet run -- --tasks 1000 --concurrency 10
```

Порівняти concurrency:

```bash
dotnet run -- --tasks 1000 --scenarios 10,50,100
```

Інший хост / ключ:

```bash
dotnet run -- --base-url http://localhost:5105 --api-key dev-api-key-change-me --tasks 1000 --concurrency 50
```

## Що вимірюється

Після кожного прогону виводиться:

- загальний час виконання;
- середній / мінімальний / максимальний час відповіді;
- кількість успішних запитів (2xx);
- кількість запитів з помилками (не-2xx або мережеві збої).

Якщо задано кілька сценаріїв — додатково таблиця порівняння.

## Що саме б’є по API

Мікс (за індексом задачі, легший на write):

| Частка | Метод | Endpoint |
|--------|--------|----------|
| 40% | GET | `/api/v1/halls` |
| 25% | GET | `/api/v1/halls/available?...` |
| 20% | GET | `/api/v1/reports/summary` |
| 10% | POST | `/api/v1/halls` |
| 5% | PUT | `/api/v1/halls/{id}` |

Спочатку виконується warmup: `GET /health` і `GET /api/v1/halls`.

### Cleanup після прогонів

За замовчуванням увімкнено (`--cleanup`):

- трекаються зали, створені через `POST` у цьому запуску;
- `PUT` виконується **лише** по них (seed Зал А/B/C не змінюються);
- в кінці — `DELETE /api/v1/halls/{id}` (soft-delete) для створених і залишків `LoadDev-*` / `LoadPut-*` (seed id не чіпає).

Вимкнути: `--no-cleanup`.  
Якщо БД уже роздута зі старих прогонів — разово `cleanup-test-halls.sql` у SSMS.

## Поради

- Перед довгими прогонами API має бути вже запущений і відповідати на `/health`.
- Для локального тесту зручний профіль **http**: `dotnet run --launch-profile http` → `http://localhost:5105` (у Development немає HTTPS-редиректу).
- Якщо б’єте в `https://localhost:7008`, тестер для localhost ігнорує помилки dev-сертифіката.
- Rate limit API: у **Development** зараз **20000**/хв (щоб 10+50+100 і cleanup вміщались у вікно); у Production — 2000.
- `POST` створює нові зали; після прогону cleanup робить soft-delete. Для hard-delete зі старих прогонів — `cleanup-test-halls.sql`.
- Зупинка: `Ctrl+C`.
