# Home

**Route:** `/` · **Status:** Implemented · **Architecture:** [docs/architecture.md](../architecture.md)

## Overview

The home page becomes a read-only, at-a-glance dashboard: small cards showing "what's true right now / this month" across every feature area, grouped into 3 categories. No filters, no editing, no navigation controls on any card — that lives on each feature's own page. Most cards start as "Скоро тут буде статистика" placeholders and get replaced with real data as each feature is built for real; only Bad Habits has a real backend today, so it's the only card with actual data at this point. This is meant to visibly grow over time without restructuring the page shape.

This replaces the current `Home.razor`, which is a generic personal-portfolio mockup (a hero banner, a "Мої навички" skills-progress block, an "Інтереси" button row) unrelated to the rest of the app — none of that carries over.

## Hero section

- An ASCII-art banner reading "DZHUS SHELTER", generated in the classic `figlet` "standard" font style and rendered as literal monospace text (a `<pre>` block, `Courier New`), styled with the same white-text + black/blue (`#209cee`) hard drop-shadow already used for the old `retro-3d-text` — keeps the "console output" look the user asked for without needing any external ASCII-art tooling at runtime (the banner text is static, generated once).
- Below the banner: an animated pixel-art sprite (`wwwroot/hero.webp` — animated WebP, transparent background, 640×360, 120 frames, ~1.6 MB) cycling through the user's hobbies (coding, car repair, hiking, tinkering/soldering). Source was a user-provided GIF with a flat black background and a "VEED" watermark that shifted between the top-left and top-right corner depending on the scene; both were stripped (border-flood-fill transparency + fixed corner-box removal, verified frame-by-frame that the character never overlaps either watermark corner) before converting to lossless animated WebP.
- `image-rendering: pixelated` on the `<img>` to keep the pixel-art crisp when scaled.

## Dashboard layout

Three category sections, each a labeled group containing a responsive card grid:

| Category | Cards |
|---|---|
| Здоров'я | Погані звички (real), Спортзал (mock), Екранний час (mock) |
| Гроші | Фінанси (mock), Курс валют (mock) |
| Інше | Погода (mock), Розклад (mock), Боти (mock) |

Every card uses the same visual language (`PixelCard`), sized small and uniform — no card gets special-cased styling because it happens to have real data.

## Bad Habits summary card (the one real card)

New component: **`BadHabitsMonthSummaryCard.razor`** (`Components/Shared/`) — wraps `PixelCard` around the existing, reusable **`PixelMonthCalendar`** (built for [bad-habits.md](bad-habits.md)'s Phase 2). No subtype filter, no month-navigation buttons, no stats list — always the current calendar month, read-only.

- Fetches once via the existing `BadHabitsApiClient.GetEntriesAsync(monthStart, monthEnd, habitType: null, subType: null, ct)` — `habitType: null` means both Alcohol and Smoking entries come back together (this is new: `PixelHabitCard` always passes a concrete `HabitType`; this card is the first caller to fetch across both).
- Marked-day icon logic (which subtype's icon wins when a day has entries for more than one distinct subtype, with a "+" badge) is identical to what `PixelHabitCard` already does — **that logic moves out of `PixelHabitCard` into a shared static helper** (e.g. on `HabitSubTypeCatalog`, or a small new static class next to it) so both components call the same tested code instead of duplicating it. This is the one refactor this feature requires; nothing else about `PixelHabitCard`/`PixelMonthCalendar`/`HabitSubTypeCatalog`'s public shape changes.

## Mock cards (7 of them)

Plain `PixelCard` per feature — `Title` is the feature name with the same emoji already used for it in `NavMenu.razor` (💰 Фінанси, 🏋️ Спортзал, 📱 Екранний час, 🌤️ Погода, 💱 Курс валют, 📅 Розклад, 🤖 Боти), body is the single line "Скоро тут буде статистика." No per-feature customization, no reuse of each page's own mock numbers — deliberately uniform so it's obvious at a glance which cards are real vs. not yet.

## Data / API

No `DzhusShelter.Api` or `DzhusShelter.TelegramBot` changes. Pure `DzhusShelter.UI` work, reusing what Bad Habits' Phase 1/2 already built.

## Open Decisions (resolved during brainstorming)

- The old hero banner (animated background image + CSS 3D text), skills progress bars, and interest buttons are all removed outright, not kept alongside the new dashboard — the user wanted a personal avatar/GIF instead, not both.
- Exchange/Schedule/Bots — pages that aren't even in the "named 5" the user first listed — get placeholder cards too, for a complete 8-card dashboard rather than a partial one.
- 3 categories (Здоров'я / Гроші / Інше) chosen over a single flat grid — confirmed with the user rather than assumed.
- Mock cards show a generic "coming soon" line rather than mirroring each page's own hardcoded mock numbers — the user explicitly preferred honesty about what's real over a busier-looking but fake dashboard.
- The Bad Habits card shows only a calendar (icons on marked days), no numeric stat and no subtype filter — deliberately the simplest possible read of "did something happen this month," leaving richer filtering to the feature's own page.
- Background removal for the hero GIF needed two different techniques across two source files: the first attempt (dithered near-white background) needed median-filter denoising + connected-component hole-absorption to avoid a speckled/haloed result; the second, cleaner source (flat black background + a repositioning watermark) only needed simple threshold flood-fill plus a verified-safe fixed corner-box mask. Neither technique is checked into the repo — the processing was a one-time offline step producing the committed `hero.webp`.

## Out of Scope

- Any real data wiring for Weather/Screen Time/Finance/Gym/Exchange/Schedule/Bots — each becomes real only when that feature itself is built (see [docs/roadmap.md](../roadmap.md)'s backlog)
- Clicking a card to navigate to its feature page (cards are informational only for now)
- Re-processing the hero GIF/webp as part of a build step — it's a static asset, edited manually if it ever needs to change
