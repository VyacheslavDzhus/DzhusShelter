# Learning Roadmap — Senior .NET Developer & AI-Engineer (2026)

Sources: the `.Net Senior Goal` text plan, and a more detailed [Excalidraw mind map](https://excalidraw.com/#json=faPgapt7mXak8IgsgWeIm,IjNp0-WmmIQ8QVWld0-7yQ) that lays the same curriculum out as an ordered path with concrete libraries and "how we practice it" notes per topic. This project is the practice vehicle — every feature in [docs/specs/](specs/README.md) is a chance to apply a topic for real, not just read about it.

## Curriculum path

This is the order the mind map lays topics out in — each topic has (a) what it covers and (b) how it's practiced, straight from the source diagram.

### 1. C#
Класи, колекції, ООП, значимі/посилкові типи, робота зі строками та файлами, делегати, винятки, узагальнення (generics), `record`, багатопотоковість, асинхронність.
*Практика:* консольні застосунки, задачки, постійна практика, писати код.

**База (інструменти/основи), паралельно з C#:** Git, GitHub, робота з консоллю, NuGet, дебаг, пошук/AI-асистенти, структури даних, прості алгоритми.
*Практика:* розвиваємо кругозір — гуглимо, вивчаємо, практикуємось.

### 2. ASP.NET Core Web API
HTTP, REST API, контролери/Minimal API, pipeline, middleware, model binding, Dependency Injection, Swagger, Postman, DTO/Requests/Responses.
*Практика:* створюємо ASP.NET Core Web API проєкти, пишемо HTTP-методи по REST, використовуємо Swagger/Postman.

Паралельні гілки, що ростуть з Web API:

- **Чиста архітектура** — Api/Infrastructure/Application/Domain шари, сервіси/хендлери, репозиторії, DI, інверсія залежностей, патерни (Декоратор, Фабрика, Репозиторій, Будівельник). *Практика:* вивчаємо чужі архітектурні рішення, дивимось інші репозиторії, просимо рев'ю, не шукаємо ідеальну архітектуру.
  - **Проєктування** — доменні області й моделі, модульність/модульна архітектура, Anemic vs Rich models, `Result` pattern. *Практика:* rich models для домену, anemic models для DTO, все валідуємо.
    - **Валідація** — `FluentValidation`, валідація вхідних параметрів, валідація в rich models, бізнес-валідація. *Практика:* валідуємо обов'язково все.

- **База даних (PostgreSQL)** — CRUD, робота з таблицями та міграції, зв'язки між таблицями, індекси, ACID, транзакції, EF Core (DbContext, ChangeTracker, LINQ), Dapper (SQL-запити), блокування, фільтри/пагінація/складні запити/join'и. *Практика:* підключаємо БД до застосунку, реалізуємо CRUD і бізнес-логіку, стежимо за індексами/транзакціями/зв'язками, постійно проєктуємо нові предметні області.
  - **CQRS** — Command pattern, CQS, Read/Write models, окремі Read/Write `DbContext`, `ICommandHandler`/`IQueryHandler`, патерн декоратор. *Практика:* поділ на команди (бізнес-логіка) і запити (читання даних), різні моделі для Read і Write.
    - **Кешування** — Memory cache, Distributed cache, Redis, стратегії кешування, інвалідація кеша. *Практика:* впроваджуємо кешування для сервісів, різні стратегії, реалізуємо інвалідацію.
      - **ElasticSearch** — повнотекстовий пошук, логування через Elastic stack. *Практика:* повнотекстовий пошук у проєкті, логи через Elastic Stack.
        - **MongoDB** → **ClickHouse** — альтернативні/додаткові сховища даних (документна БД, колонкова БД для аналітики) — згадані як окремі теми для вивчення, без деталізації в мапі.
      - **Логування** — Serilog, Seq, Elastic, рівні логування, конфігурація логування. *Практика:* впроваджуємо логування в проєкт, логи всюди.
        - **Auth** — автентифікація, авторизація, сесії і токени, JWT, ролі/дозволи/повноваження (RBAC, ABAC), `asp net core Identity`, OAuth, OpenIdConnect, SSO, Identity Provider, Keycloak, автентифікація/авторизація з боку клієнта. *Практика:* спочатку самописна автентифікація/авторизація в проєкті, потім тренуємось з Keycloak.
          - **Взаємодія з фронтом** — CORS, NGINX, HTTP, REST API, OpenAPI, Cookie, Headers. *Практика:* в ідеалі робимо просте фронтенд-застосунок і з'єднуємо його з бекендом.

### 3. Docker
Контейнери, Docker Compose. *Практика:* запускаємо Postgres/Redis/інші сервіси в докері, піднімаємо своє Web API в докері, завжди `docker-compose up -d`. Реальна ціль для деплою — домашній сервер на CasaOS (див. `deploy-dzhus-shelter` скіл), тож `docker-compose.yml` цього репо — не навчальна вправа, а те, що реально піде в продакшн.

- **Kubernetes** — позначено як наступний крок після Docker, без деталізації в мапі (посилання: [roadmap.sh/kubernetes](https://roadmap.sh/kubernetes)).

### 4. Тестування
Юніт-тести, інтеграційні тести, end-to-end тести, xUnit, Moq/NSubstitute, Testcontainers, Respawn, AutoFixture, FluentAssertions. *Практика:* обов'язково пишемо тести для бізнес-логіки, інтеграційні тести перевіряють роботу застосунку та зовнішніх систем.

### 5. Фонові процеси
`Host`, `IHostedService`, `BackgroundService`, Channels, Quartz, Hangfire. *Практика:* придумуємо функціонал, який виконується періодично у фоні.

- **WebSockets/SignalR** — передача даних у реальному часі (сповіщення), live-оновлення (черги, графіки, статуси), онлайн-чат.

### 6. Робота з файлами
S3, Amazon S3 Client, MinIO, presigned-посилання, multipart-завантаження, валідація файлів. *Практика:* додаємо файли в проєкт (аватарки, скріншоти, відео), все зберігаємо в S3.

- **Обробка файлів** — FFmpeg для відеообробки, робота з PDF (QuestPDF, PdfSharp), робота з Excel (EPPlus, ClosedXML), ZIP/RAR, робота з файлами великого розміру (Stream). *Практика:* звіти, аудити, генерація файлів.

### 7. Брокери повідомлень
RabbitMQ, Kafka, подійно-орієнтована архітектура, інтеграційні події, Pub/Sub, Producers/Consumers, Outbox патерн, Dead letter queue. *Практика:* пишемо другий сервіс, що слухає черги/топіки, куди перший сервіс надсилає повідомлення.

- **Мікросервіси** — брокери повідомлень, контейнеризація, синхронна/асинхронна взаємодія, HTTP/gRPC-комунікація, Vertical Slice Design. *Практика:* розбиваємо моноліт на частини (або одразу проєктуємо модульний застосунок і ділимо на модулі), продумуємо взаємодію сервісів, вивчаємо популярні патерни мікросервісної архітектури.
  - **Патерни в мікросервісах і архітектурі** — DDD, API Gateway, BFF (Backend for Frontend), Circuit Breaker, Retry, Rate Limiting, Database per Service, Saga, Event Sourcing, розподілений трейсинг/логування, автентифікація в мікросервісах, Keycloak. *Практика:* вивчаємо ці патерни, акуратно й поступово впроваджуємо їх у мікросервісах.

### 8. DDD (окрема поглиблена гілка)
Предметна область (Domain), Bounded Context, Entity, Value Objects, Aggregate, доменні події. *Практика:* проєктуємо і розробляємо сервіс за допомогою DDD.

### 9. Моніторинг
Observability, логування, трейсинг, метрики: Elastic, Grafana, Kibana, Prometheus, OpenTelemetry, Jaeger. *Практика:* впроваджуємо моніторинг у всі сервіси, все піднімаємо в докері.

### 10. Frontend / React (stretch, поза основним фокусом)
Позначено лише посиланнями без деталізації: [roadmap.sh/frontend](https://roadmap.sh/frontend), [roadmap.sh/react](https://roadmap.sh/react).

### 11. AI-Assisted Development
З текстового плану (ще не додано до mind map):
- Spec-Driven Development з агентами — цей репо вже практикує це через `docs/specs/` + скіли
- Створення власних MCP-серверів на C#, щоб надати агенту доступ до локальних систем/БД/платформи
- Агентні воркфлоу проти issue-трекера: агент сам створює гілки/worktree, пише код, комітить, відкриває MR

## How Claude works with you on this

1. Code examples target **.NET 10** (minimum .NET 8/9 where 10-specific APIs don't apply yet)
2. Every architectural choice comes with **why**, plus its trade-offs — not just the code
3. Features are introduced **incrementally**: start as a simple modular monolith, graduate pieces toward microservice patterns only once the simpler version is understood and working
4. Claude proactively reviews your code for performance and security issues, not just whether it runs

## Suggested progression against DzhusShelter features

Rough order — simple CRUD first to nail down Clean Architecture basics, then layer in the harder topics feature by feature. Not a fixed contract; re-order as you go.

| Order | Feature | Curriculum focus |
|---|---|---|
| 1 | **Bad Habits** — done, see [docs/architecture.md](architecture.md) | Clean Architecture skeleton (Api/Infrastructure/Application/Domain) split into `DzhusShelter.Api`/`UI`/`TelegramBot`, rich domain models, CQRS from day one (bot writes/Blazor reads split makes it natural), FluentValidation, EF Core + PostgreSQL, Blazor-ApexCharts, first `Testcontainers` integration test. CQRS pulled forward from step 2 below — see architecture doc |
| 1b | Gym, Schedule | Same Clean Architecture shape as Bad Habits, reusing `DzhusShelter.Api`; simpler CRUD without necessarily needing CQRS (plain service is fine unless a read/write split shows up naturally) |
| 2 | Finance | FluentValidation, Monobank API integration behind a resilient HTTP client (Polly retry) — CQRS pattern already established by Bad Habits |
| 3 | Exchange, Weather | Dapper for computed queries, Redis / distributed cache for rate & forecast caching, cache invalidation strategy |
| 4 | Screen Time | First externally-callable ingest endpoint — auth, rate limiting; candidate for an Outbox + domain event once ingestion needs to trigger other work |
| 5 | Bots | RabbitMQ/event-driven send, Outbox pattern for reliable Telegram delivery, Circuit Breaker around the Telegram API call; Bots' "message log" view is also a natural fit for SignalR live updates |
| 6 (stretch) | Cross-cutting | Serilog + Elastic/Grafana/Prometheus/OpenTelemetry across all features, Auth (JWT → Keycloak), CI/CD pipeline, first custom MCP server exposing this app's data to an agent |

## Progress log

Update this as topics get exercised for real (not just discussed):

- **2026-09-21** — [docs/architecture.md](architecture.md) written: DzhusShelter splits into `DzhusShelter.Api` (Clean Architecture + CQRS) / `DzhusShelter.UI` / `DzhusShelter.TelegramBot`. Bad Habits ([specs/bad-habits.md](specs/bad-habits.md)) is the first feature built this way.
- **2026-09-21** — Bad Habits implemented end-to-end and verified live (real Telegram chat, real browser, full `docker-compose` stack — not just unit tests): `DzhusShelter.Api` (Clean Architecture + CQRS + `Result` pattern + FluentValidation + EF Core/PostgreSQL), `DzhusShelter.TelegramBot` (long-polling bot with a root-menu → habit-type → sub-type flow), `DzhusShelter.UI` (Blazor-ApexCharts dashboard with filters). First real use of `Testcontainers`. Topics exercised for real: Clean Architecture, CQRS, Result pattern, FluentValidation, PostgreSQL/EF Core, Docker Compose multi-service, xUnit/NSubstitute/Testcontainers. Notable bugs found only by actually running things (not by compiling): EF Core package version drift across projects, silent JSON case-sensitivity/enum-converter mismatches on HTTP clients, a `DateTimeOffset`/`DateTimeKind.Local` mismatch, a Blazor page missing `@rendermode` (silently non-interactive), and `ApexChart` not repainting on data changes without a `@key`.
