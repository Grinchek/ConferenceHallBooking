# Навантажувальне тестування (dev)

Консольний клієнт, який емулює одночасну роботу багатьох клієнтів Web API: GET / POST / PUT, асинхронно, з обмеженням паралельності та збором статистики.

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
| `--concurrency` | `10` | Максимум одночасних HTTP-запитів |
| `--scenarios` | — | Кілька рівнів concurrency через кому, напр. `10,50,100` |
| `--engine` | `semaphore` | `semaphore` або `foreach` |
| `--help` | — | Довідка |

Якщо задано `--scenarios`, значення `--concurrency` для прогонів не використовується — ганяються всі перелічені рівні підряд.

### Режими (`--engine`)

- **`semaphore`** (основний) — створюється N задач (`Task.WhenAll`), одночасність обмежує `SemaphoreSlim`.
- **`foreach`** — той самий workload через `Parallel.ForEachAsync` з `MaxDegreeOfParallelism`.

## Приклади (відповідають ТЗ)

Мінімум: 1000 задач / 10 concurrent (semaphore):

```bash
dotnet run -- --tasks 1000 --concurrency 10
```

Додаткове завдання — порівняти concurrency:

```bash
dotnet run -- --tasks 1000 --scenarios 10,50,100
```

Те саме в режимі `foreach`:

```bash
dotnet run -- --engine foreach --tasks 1000 --scenarios 10,50,100
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

Рівномірний мікс (за індексом задачі):

1. `GET /api/v1/halls`
2. `GET /api/v1/halls/available?...`
3. `GET /api/v1/reports/summary`
4. `POST /api/v1/halls` (унікальна назва `LoadDev-...`)
5. `PUT /api/v1/halls/{id}` (id з warmup `GET /halls`)

Спочатку виконується warmup: `GET /health` і `GET /api/v1/halls`.

## Поради

- Перед довгими прогонами API має бути вже запущений і відповідати на `/health`.
- Rate limit API зараз **2000** запитів/хв на IP — для сценаріїв `10/50/100 × 1000` задач робіть паузу між повними порівняльними прогонами або ганяйте сценарії окремо, якщо бачите багато помилок.
- `POST` створює нові зали в БД; `PUT` змінює існуючі — для shared/dev БД це очікувано.
- Зупинка: `Ctrl+C`.
