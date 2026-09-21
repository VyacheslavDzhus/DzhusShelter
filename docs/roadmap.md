# Learning Roadmap — Senior .NET Developer & AI-Engineer (2026)

Source: `.Net Senior Goal` plan. This project is the practice vehicle for the curriculum below — every feature in [docs/specs/](specs/README.md) is a chance to apply one or more of these topics for real, not just read about them.

## Curriculum

### 1. Architecture & Design
- Clean Architecture & DDD: rich domain models (not anemic), bounded contexts, aggregates
- Patterns: CQRS, repository, `Result` pattern instead of exceptions for business-rule failures
- ASP.NET Core pipeline: custom middleware (e.g. exception-handling middleware), filters
- Validation: `FluentValidation` combined with DDD; custom validation factories and extension methods that collect errors via a validation context

### 2. Engineering Practices & Databases
- Transaction management: Transaction Manager, Outbox pattern, domain event dispatching
- Databases: PostgreSQL as primary relational store; EF Core (query optimization, migrations, Fluent API) and Dapper for complex SQL
- Caching: Redis as distributed cache, .NET 9+ `HybridCache`, cache invalidation
- Testing: integration tests with `Testcontainers` (Postgres/Redis/RabbitMQ in Docker) and `Respawn` for DB reset between tests

### 3. Microservices & Async Communication
- Message brokers: RabbitMQ, Kafka; event-driven architecture
- Microservice patterns: API Gateway, BFF, Circuit Breaker, Retry, Rate Limiting, Saga
- File handling: S3-compatible storage (MinIO), multipart uploads, presigned URLs

### 4. Infrastructure, Cloud & Observability
- Containers/orchestration: Docker, Docker Compose, intro to Kubernetes, .NET Aspire for orchestration/service discovery
- Observability: OpenTelemetry (metrics/logs/traces), Prometheus, Tempo, Grafana, ElasticSearch
- CI/CD: GitLab CI or GitHub Actions — build, test, deploy pipelines

### 5. AI-Assisted Development
- Spec-Driven Development with agents (this repo's `docs/specs/` + skills workflow already practices this)
- Building custom MCP servers in C# to give an agent access to local systems/DB/platform
- Agent-driven workflows against an issue tracker: agent creates branches/worktrees, writes code, commits, opens MRs

## How Claude works with you on this

1. Code examples target **.NET 10** (minimum .NET 8/9 where 10-specific APIs don't apply yet)
2. Every architectural choice comes with **why**, plus its trade-offs — not just the code
3. Features are introduced **incrementally**: start as a simple modular monolith, graduate pieces toward microservice patterns only once the simpler version is understood and working
4. Claude proactively reviews your code for performance and security issues, not just whether it runs

## Suggested progression against DzhusShelter features

Rough order — simple CRUD first to nail down Clean Architecture basics, then layer in the harder topics feature by feature. Not a fixed contract; re-order as you go.

| Order | Feature | Curriculum focus |
|---|---|---|
| 1 | Gym, Bad Habits, Schedule | Clean Architecture skeleton, rich domain models, `Result` pattern, EF Core + PostgreSQL basics, first `Testcontainers` integration tests |
| 2 | Finance | CQRS (commands to log/sync, queries to read balances), FluentValidation, Monobank API integration behind a resilient HTTP client (Polly retry) |
| 3 | Exchange, Weather | Dapper for computed queries, Redis / `HybridCache` for rate & forecast caching, cache invalidation |
| 4 | Screen Time | First externally-callable ingest endpoint — auth, rate limiting; candidate for an Outbox + domain event once ingestion needs to trigger other work |
| 5 | Bots | RabbitMQ/event-driven send, Outbox pattern for reliable Telegram delivery, Circuit Breaker around the Telegram API call |
| 6 (stretch) | Cross-cutting | OpenTelemetry + Grafana/Prometheus across all features, CI/CD pipeline, first custom MCP server exposing this app's data to an agent |

## Progress log

Update this as topics get exercised for real (not just discussed):

- _(nothing implemented yet — mockups only, see [docs/specs/README.md](specs/README.md))_
