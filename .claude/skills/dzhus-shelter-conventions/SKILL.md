---
name: dzhus-shelter-conventions
description: Use when writing or reviewing any code in the DzhusShelter repo (Blazor Server, .NET 10) — conventions for pages, layout, styling, and where specs live.
---

# Dzhus Shelter Conventions

Dzhus Shelter is a personal dashboard, no auth — single user. As of [docs/architecture.md](../../../docs/architecture.md), it's split across three projects — see that doc before assuming "Blazor does everything." Feature pages not yet rebuilt against that architecture are still static mockups in the original single-project Blazor app; real data/logic gets added incrementally, one feature at a time, each backed by a spec.

## Project layout

All projects live under `src/`, one folder per project (see `docs/superpowers/plans/2026-09-21-bad-habits-implementation.md` for why — the UI project used to sit directly at the repo root, which made new sibling projects look like they were nested inside it):

- `src/DzhusShelter.UI/` (Blazor Server, .NET 10, interactive server render mode) — presentation only for features built after the architecture doc; calls `DzhusShelter.Api` over HTTP, no direct DB access
  - `Components/Pages/*.razor` — one file per route/feature (`@page "/route"`)
  - `Components/Layout/` — `MainLayout.razor`, `NavMenu.razor` (route list), `ReconnectModal.razor`
  - `Components/Shared/` — reusable components (e.g. `PixelCard.razor`)
  - `wwwroot/` — static assets, `app.css`, NES.css theme
- `src/DzhusShelter.Api/` — Clean Architecture (Domain/Application/Infrastructure/Controllers), CQRS, EF Core + PostgreSQL, one controller per feature
- `src/DzhusShelter.TelegramBot/` — bot worker process, calls `DzhusShelter.Api` for writes
- `docs/specs/` — one spec per feature, written before building it (see `add-feature-page` skill)
- `docs/architecture.md` — cross-cutting decisions (why three projects, CQRS convention, security posture) that apply to every feature built after it was written

## Page conventions

- Every page starts with `@page "/route-name"` then `<PageTitle>Name</PageTitle>`
- Register every new route as a `<NavLink>` entry in [NavMenu.razor](../../../src/DzhusShelter.UI/Components/Layout/NavMenu.razor) with an emoji icon, matching the existing entries' style
- UI is built from NES.css classes (`nes-container`, `nes-btn`, `nes-progress`) and the shared [PixelCard.razor](../../../src/DzhusShelter.UI/Components/Shared/PixelCard.razor) component — reuse these instead of inventing new containers
- Component-scoped styles go in a sibling `.razor.css` file (see `MainLayout.razor.css`, `PixelCard.razor.css`), not inline `<style>` blocks, except for one-off page hero sections (see `Home.razor` for the current exception)

## C# / .NET conventions

- Target framework: `net10.0`, `Nullable` and `ImplicitUsings` are both `enable` — don't add null-forgiving hacks, model nullability honestly
- `BlazorDisableThrowNavigationException` is set — navigation-related exceptions are suppressed by design, don't reintroduce try/catch around navigation for that reason

## Before building a feature

Check `docs/specs/<feature>.md` first. If it doesn't exist yet or is marked with open decisions, resolve those with the user before writing code — don't guess at data models or API choices silently.
