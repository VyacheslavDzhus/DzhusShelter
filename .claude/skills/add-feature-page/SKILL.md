---
name: add-feature-page
description: Use when turning a feature spec in docs/specs/ into real, working code in the DzhusShelter dashboard — checklist from spec to shipped page.
---

# Add Feature Page

Turns a spec at `docs/specs/<feature>.md` into a real implementation, replacing the current static mockup. Follow `dzhus-shelter-conventions` for styling/layout, and [docs/architecture.md](../../../docs/architecture.md) for how the three projects fit together — every feature built after that doc was written goes through `DzhusShelter.Api`, not directly into the Blazor project.

## Checklist

1. **Read the spec** — `docs/specs/<feature>.md`. If it has an "Open Decisions" section with unresolved items that block this work, stop and resolve them with the user first (don't silently pick a default).
2. **Domain** — add the rich entity/entities described in the spec's Data Model section, in `DzhusShelter.Api`'s Domain layer, with invariants enforced in the constructor.
3. **Application (CQRS)** — add the command(s)/query(-ies) described in the spec's Application section, each with an `ICommandHandler`/`IQueryHandler` and `FluentValidation` validator.
4. **Infrastructure** — EF Core entity configuration + migration for PostgreSQL (or the persistence the spec names, if different).
5. **API** — add the controller/endpoints from the spec's API section to `DzhusShelter.Api`, dispatching to the command/query handlers.
6. **Wire secrets/config** — API keys or connection strings go in `appsettings.Development.json` (local) and are documented as required env vars in the spec; never hardcode them.
7. **Client(s)** — update `Components/Pages/<Feature>.razor` in `DzhusShelter.UI` to call the API via `HttpClient` instead of hardcoded values (keep the existing PixelCard/nes-container layout unless the spec's UI section says otherwise), and/or add the bot flow to `DzhusShelter.TelegramBot` if the spec describes one.
8. **Update the spec status** — change the spec's status line (Spec'd → Implemented) once it reflects real data end-to-end.
9. **Update `docs/specs/README.md`** — flip that feature's status in the status table.

## When the spec doesn't exist yet

Don't start coding. Use the brainstorming skill to work out the feature with the user and write the spec first — this repo builds one feature at a time, spec before code.
