# Conference Hall Booking API

ASP.NET Core Web API для управління конференц-залами, бронюваннями та розрахунку вартості оренди.

## Стек

| Шар | Технології |
|-----|------------|
| API | ASP.NET Core 10, Swagger/OpenAPI, Rate Limiting, API Key |
| Application | FluentValidation, сервіси застосунку |
| Domain | Сутності, правила тарифікації |
| Infrastructure | EF Core + SQLite |
| Tests | xUnit |

Архітектура — **Clean Architecture** (Domain → Application → Infrastructure / Api), щоб проєкт було легко масштабувати.

## Швидкий старт

```bash
# Вимоги: .NET 10 SDK
cd src/ConferenceHallBooking.Api
dotnet run
```

Після запуску:

- Swagger UI: `https://localhost:7xxx/swagger` (порт дивіться в консолі / `launchSettings.json`)
- Health: `GET /health`
- API Key (за замовчуванням): `dev-api-key-change-me` — заголовок `X-Api-Key`

Змініть ключ у `appsettings.json` → `Security:ApiKey` перед продакшеном.

## Початкові дані (seed)

При першому запуску створюються зали:

| Зал | Місткість | Базова ставка (грн/год) | Послуги |
|-----|-----------|-------------------------|---------|
| Зал А | 50 | 2000 | Проєктор 500, Wi-Fi 300, Звук 700 |
| Зал B | 100 | 3500 | те саме |
| Зал C | 30 | 1500 | те саме |

## Тарифікація оренди залу

Вартість рахується **похвилинно** з розбиттям на тарифні сегменти:

| Період | Години | Коефіцієнт |
|--------|--------|------------|
| Morning | 06:00–09:00 | ×0.90 (−10%) |
| Standard | 09:00–18:00 | ×1.00 |
| **Peak** | **12:00–14:00** | **×1.15 (+15%)** — пріоритет над Standard |
| Evening | 18:00–23:00 | ×0.80 (−20%) |
| OffHours | 23:00–06:00 | ×1.00 |

Додаткові послуги — **фіксована** вартість за бронювання (не за годину).

**Приклад:** Зал А, 10:00–14:00  
`2 год × 2000 (Standard) + 2 год × 2300 (Peak) = 8600 грн` + обрані послуги.

## API Endpoints

Усі запити (крім `/swagger` і `/health`) потребують заголовка:

```http
X-Api-Key: dev-api-key-change-me
```

### Зали

| Метод | Шлях | Опис |
|-------|------|------|
| `POST` | `/api/v1/halls` | Додати зал |
| `PUT` | `/api/v1/halls/{id}` | Редагувати зал |
| `DELETE` | `/api/v1/halls/{id}` | Видалити зал (soft-delete) |
| `GET` | `/api/v1/halls` | Список залів |
| `GET` | `/api/v1/halls/{id}` | Зал за ID |
| `GET` | `/api/v1/halls/available?start=&end=&requiredCapacity=` | Пошук доступних |

### Бронювання

| Метод | Шлях | Опис |
|-------|------|------|
| `POST` | `/api/v1/bookings` | Забронювати + розрахунок вартості |
| `GET` | `/api/v1/bookings/{id}` | Деталі бронювання |
| `POST` | `/api/v1/bookings/{id}/cancel` | Скасувати |

### Звіти / аналітика

| Метод | Шлях | Опис |
|-------|------|------|
| `GET` | `/api/v1/reports/summary` | Зведений dashboard |
| `GET` | `/api/v1/reports/revenue-by-hall` | Виручка по залах |
| `GET` | `/api/v1/reports/occupancy` | Завантаженість |
| `GET` | `/api/v1/reports/popular-services` | Популярні послуги |

Опційні query-параметри звітів: `from`, `to` (ISO-8601).

## Приклади запитів

### Створити зал

```http
POST /api/v1/halls
X-Api-Key: dev-api-key-change-me
Content-Type: application/json

{
  "name": "Зал D",
  "capacity": 40,
  "baseHourlyRate": 1800,
  "services": [
    { "name": "Проєктор", "price": 500 },
    { "name": "Wi-Fi", "price": 300 }
  ]
}
```

### Пошук доступних

```http
GET /api/v1/halls/available?start=2024-09-01T10:00:00&end=2024-09-01T14:00:00&requiredCapacity=50
X-Api-Key: dev-api-key-change-me
```

### Бронювання

```http
POST /api/v1/bookings
X-Api-Key: dev-api-key-change-me
Content-Type: application/json

{
  "hallId": "<guid залу>",
  "start": "2024-09-01T10:00:00",
  "end": "2024-09-01T14:00:00",
  "selectedServices": ["Проєктор", "Wi-Fi"],
  "customerName": "ТОВ Приклад"
}
```

Відповідь містить `hallRentalCost`, `servicesCost`, `totalCost` та `pricingBreakdown`.

## Безпека та відмовостійкість

- **API Key** (`X-Api-Key`) з порівнянням у fixed-time (захист від timing-атак)
- **FluentValidation** на всі вхідні DTO
- **Централізований exception middleware** — єдиний JSON-формат помилок
- **Rate limiting** — до 100 запитів/хв на IP
- **Soft-delete** залів — історія бронювань зберігається
- **Знімок послуг** у бронюванні — зміна прайсу залу не ламає минулі рахунки
- **Перевірка перетину** інтервалів бронювання
- **Health check** `/health` для моніторингу
- HTTPS redirection, CORS (обмежений у Production)

## Структура рішення

```
ConferenceHallBooking/
├── src/
│   ├── ConferenceHallBooking.Api/             # Controllers, middleware, Swagger
│   ├── ConferenceHallBooking.Application/     # Use-cases, DTOs, validators
│   ├── ConferenceHallBooking.Domain/          # Entities, PricingCalculator
│   └── ConferenceHallBooking.Infrastructure/  # EF Core, seed, repositories
├── tests/
│   └── ConferenceHallBooking.Tests/           # Unit-тести тарифікації
└── README.md
```

## Тести

```bash
dotnet test
```

Покривають ключові сценарії `PricingCalculator` (standard / morning / evening / peak / mixed).

## Подальший розвиток

- JWT / ролі (Admin, Manager, Client) замість API Key
- PostgreSQL / SQL Server замість SQLite
- Outbox + повідомлення про підтвердження бронювання
- Ідемпотентність `POST /bookings`
- Календар доступності та concurrency token на бронюваннях
