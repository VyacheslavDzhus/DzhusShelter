---
name: dzhus-shelter-conventions
description: Use when writing or reviewing any code in the DzhusShelter repo (Blazor Server, .NET 10) — conventions for pages, layout, styling, and where specs live.
---

# Dzhus Shelter Conventions

Dzhus Shelter is a personal Blazor Server dashboard (.NET 10, interactive server render mode). It has no auth — single user. Feature pages (Finance, Gym, Weather, etc.) are currently static mockups; real data/logic gets added incrementally, one feature at a time, each backed by a spec.

## Project layout

- `Components/Pages/*.razor` — one file per route/feature (`@page "/route"`)
- `Components/Layout/` — `MainLayout.razor`, `NavMenu.razor` (route list), `ReconnectModal.razor`
- `Components/Shared/` — reusable components (e.g. `PixelCard.razor`)
- `docs/specs/` — one spec per feature, written before building it (see `add-feature-page` skill)
- `wwwroot/` — static assets, `app.css`, NES.css theme

## Page conventions

- Every page starts with `@page "/route-name"` then `<PageTitle>Name</PageTitle>`
- Register every new route as a `<NavLink>` entry in [NavMenu.razor](../../../Components/Layout/NavMenu.razor) with an emoji icon, matching the existing entries' style
- UI is built from NES.css classes (`nes-container`, `nes-btn`, `nes-progress`) and the shared [PixelCard.razor](../../../Components/Shared/PixelCard.razor) component — reuse these instead of inventing new containers
- Component-scoped styles go in a sibling `.razor.css` file (see `MainLayout.razor.css`, `PixelCard.razor.css`), not inline `<style>` blocks, except for one-off page hero sections (see `Home.razor` for the current exception)

## C# / .NET conventions

- Target framework: `net10.0`, `Nullable` and `ImplicitUsings` are both `enable` — don't add null-forgiving hacks, model nullability honestly
- `BlazorDisableThrowNavigationException` is set — navigation-related exceptions are suppressed by design, don't reintroduce try/catch around navigation for that reason

## Before building a feature

Check `docs/specs/<feature>.md` first. If it doesn't exist yet or is marked with open decisions, resolve those with the user before writing code — don't guess at data models or API choices silently.
