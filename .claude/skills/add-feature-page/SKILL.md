---
name: add-feature-page
description: Use when turning a feature spec in docs/specs/ into real, working code in the DzhusShelter dashboard — checklist from spec to shipped page.
---

# Add Feature Page

Turns a spec at `docs/specs/<feature>.md` into a real implementation, replacing the current static mockup. Follow `dzhus-shelter-conventions` for styling/layout while doing this.

## Checklist

1. **Read the spec** — `docs/specs/<feature>.md`. If it has an "Open Decisions" section with unresolved items that block this work, stop and resolve them with the user first (don't silently pick a default).
2. **Data model** — add the entities described in the spec's Data Model section. Follow whatever persistence the spec names (EF Core + PostgreSQL, MongoDB, or external API only — see the spec's Persistence line).
3. **Service layer** — add a service/repository behind an interface for the data access or external API call described in the spec's Integrations section. Register it in `Program.cs`.
4. **Wire secrets/config** — API keys or connection strings go in `appsettings.Development.json` (local) and are documented as required env vars in the spec; never hardcode them in a `.razor` file.
5. **Replace the mockup** — update `Components/Pages/<Feature>.razor` to use the real service instead of hardcoded values, keeping the existing PixelCard/nes-container layout unless the spec's UI section says otherwise.
6. **Update the spec status** — change the spec's status line (Draft/Mockup → Implemented) once the page reflects real data.
7. **Update `docs/specs/README.md`** — flip that feature's status in the roadmap table.

## When the spec doesn't exist yet

Don't start coding. Use the brainstorming skill to work out the feature with the user and write the spec first — this repo builds one feature at a time, spec before code.
