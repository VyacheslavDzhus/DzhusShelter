# Dzhus Shelter

Personal Blazor Server dashboard (.NET 10) — see `.claude/skills/dzhus-shelter-conventions` for code conventions and `docs/specs/` for per-feature specs.

## This is also a learning project

The user is working toward **Senior .NET Developer & AI-Engineer** level (target: 2026) and uses this repo as the practice vehicle — see [docs/roadmap.md](docs/roadmap.md) for the full curriculum (Clean Architecture/DDD, CQRS, EF Core/Dapper/Postgres, Redis/HybridCache, RabbitMQ/Kafka, microservice patterns, Docker/Aspire/K8s, observability, CI/CD, MCP servers/agent-driven dev) and the suggested feature-to-topic progression.

When building or discussing a feature here:

1. Target **.NET 10** in examples (min .NET 8/9 where a 10-only API doesn't apply)
2. Explain the **why** behind an architectural choice and its trade-offs, not just the code
3. Introduce complexity **incrementally** — simple modular monolith first, graduate pieces toward the harder patterns (CQRS, messaging, microservices) once the simpler version works
4. Proactively flag performance and security issues in code you review, don't just confirm it runs
5. Before implementing a feature, check `docs/specs/<feature>.md` — resolve open decisions with the user rather than guessing, and update `docs/roadmap.md`'s progress log once a curriculum topic is genuinely exercised (not just discussed)
