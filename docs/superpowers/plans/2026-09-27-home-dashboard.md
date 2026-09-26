# Home Dashboard Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the generic portfolio-mockup `Home.razor` with a read-only dashboard — an ASCII-art hero banner + animated pixel-art sprite, and 3 categories of small stat cards, one of which (Bad Habits) shows real data via a new mini-calendar card.

**Architecture:** Pure `DzhusShelter.UI` change — no Api or Bot changes. One small refactor first (move `PixelHabitCard`'s private marked-days-building logic into a public static helper on `HabitSubTypeCatalog` so a second component can reuse it), then a new component (`BadHabitsMonthSummaryCard`) that reuses the already-built `PixelMonthCalendar`, then the `Home.razor` rewrite itself.

**Tech Stack:** .NET 10, Blazor Server (interactive server render mode not required here — this page has no interactivity), NES.css, existing `BadHabitsApiClient`/`HabitEntryDto`/`PixelCard`/`PixelMonthCalendar`/`HabitSubTypeCatalog` (no changes to their public shape except the one addition in Task 1).

**Spec:** [docs/specs/home.md](../../specs/home.md)

## Global Constraints

- No `DzhusShelter.Api` or `DzhusShelter.TelegramBot` changes at all — every task touches only `src/DzhusShelter.UI/`.
- Every card on this page is read-only: no filters, no navigation, no editing controls anywhere on `/`.
- Mock cards all show the exact same line, `"Скоро тут буде статистика."` — never each page's own mock numbers.
- No new `DzhusShelter.UI` test project — this codebase has no automated tests for any Blazor component (see the two prior Bad Habits UI plans); verification here is build success plus one manual browser pass at the end.
- `wwwroot/hero.webp` already exists in the repo (committed in an earlier commit, `577f69d`) — no task in this plan creates or processes it.

---

### Task 1: Extract the marked-days builder into a shared, public helper

**Files:**
- Modify: `src/DzhusShelter.UI/Components/Shared/HabitSubTypeCatalog.cs`
- Modify: `src/DzhusShelter.UI/Components/Shared/PixelHabitCard.razor`

**Interfaces:**
- Produces: `HabitSubTypeCatalog.BuildMarkedDays(IEnumerable<HabitEntryDto> entries) : Dictionary<DateOnly, MarkupString>` — a public static method, moved verbatim (same logic, same output) from `PixelHabitCard`'s existing private method of the same name. Task 3 (via the new `BadHabitsMonthSummaryCard`, Task 2) depends on this being public and living on `HabitSubTypeCatalog`.

This is a pure refactor — same behavior, same output, just relocated so a second component (Task 2) doesn't have to duplicate it. `PixelHabitCard`'s own behavior must not change.

- [ ] **Step 1: Add the method to `HabitSubTypeCatalog.cs`**

Add `using Microsoft.AspNetCore.Components;` to the top of the file (needed for `MarkupString`), and add this public static method to the `HabitSubTypeCatalog` class (anywhere inside the class body — e.g. right after `IconSvg`):

```csharp
public static Dictionary<DateOnly, MarkupString> BuildMarkedDays(IEnumerable<HabitEntryDto> entries)
{
    var result = new Dictionary<DateOnly, MarkupString>();
    foreach (var group in entries.GroupBy(e => DateOnly.FromDateTime(e.OccurredAt.UtcDateTime.Date)))
    {
        var distinctSubTypes = group.Select(e => e.SubType).Distinct().OrderBy(s => (int)s).ToList();
        var primaryIcon = IconSvg(distinctSubTypes[0]);
        var markup = distinctSubTypes.Count > 1
            ? $"<div class=\"pixel-calendar-marker\">{primaryIcon}<span class=\"pixel-calendar-plus\">+</span></div>"
            : $"<div class=\"pixel-calendar-marker\">{primaryIcon}</div>";
        result[group.Key] = new MarkupString(markup);
    }
    return result;
}
```

(Note: inside this class, `IconSvg` is called unqualified since it's the same class's own static method — unlike `PixelHabitCard`'s old version, which called it as `HabitSubTypeCatalog.IconSvg(...)`.)

- [ ] **Step 2: Remove the duplicate from `PixelHabitCard.razor` and call the shared one**

In `src/DzhusShelter.UI/Components/Shared/PixelHabitCard.razor`, delete the entire private `BuildMarkedDays` method from the `@code` block (it currently sits right after `ComputeStats`), and change the one call site in `ReloadAsync` from:

```csharp
_markedDays = BuildMarkedDays(_entries.Where(e => e.OccurredAt >= monthStart && e.OccurredAt <= monthEnd));
```

to:

```csharp
_markedDays = HabitSubTypeCatalog.BuildMarkedDays(_entries.Where(e => e.OccurredAt >= monthStart && e.OccurredAt <= monthEnd));
```

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build src/DzhusShelter.UI/DzhusShelter.UI.csproj`
Expected: 0 errors. (No automated test exists for either file — verify by reading the diff that the moved method's body is byte-identical to what was removed, just relocated and with `IconSvg` called unqualified.)

- [ ] **Step 4: Commit**

```bash
git add src/DzhusShelter.UI/Components/Shared/HabitSubTypeCatalog.cs src/DzhusShelter.UI/Components/Shared/PixelHabitCard.razor
git commit -m "refactor(ui): move BuildMarkedDays onto HabitSubTypeCatalog for reuse"
```

---

### Task 2: BadHabitsMonthSummaryCard — the real Home-page card

**Files:**
- Create: `src/DzhusShelter.UI/Components/Shared/BadHabitsMonthSummaryCard.razor`

**Interfaces:**
- Consumes: `HabitSubTypeCatalog.BuildMarkedDays(IEnumerable<HabitEntryDto>)` (Task 1), `BadHabitsApiClient.GetEntriesAsync(DateTimeOffset, DateTimeOffset, HabitType?, HabitSubType?, CancellationToken)` (existing, `src/DzhusShelter.UI/Services/BadHabitsApiClient.cs` — unchanged), `<PixelMonthCalendar Year Month MarkedDays>` (existing, unchanged), `<PixelCard Title>` (existing, unchanged).
- Produces: `<BadHabitsMonthSummaryCard />` — no parameters at all. Task 3 depends on this exact usage (no attributes).

- [ ] **Step 1: Create the component**

```razor
@using DzhusShelter.UI.Services
@inject BadHabitsApiClient ApiClient

<PixelCard Title="Погані звички">
    @if (_markedDays is null)
    {
        <p><em>Завантаження...</em></p>
    }
    else
    {
        <PixelMonthCalendar Year="_year" Month="_month" MarkedDays="_markedDays" />
    }
</PixelCard>

@code {
    private readonly int _year = DateTime.UtcNow.Year;
    private readonly int _month = DateTime.UtcNow.Month;
    private Dictionary<DateOnly, MarkupString>? _markedDays;

    protected override async Task OnInitializedAsync()
    {
        var monthStart = new DateTimeOffset(new DateTime(_year, _month, 1), TimeSpan.Zero);
        var monthEnd = monthStart.AddMonths(1).AddTicks(-1);

        var entries = await ApiClient.GetEntriesAsync(monthStart, monthEnd, null, null, CancellationToken.None);
        _markedDays = HabitSubTypeCatalog.BuildMarkedDays(entries);
    }
}
```

No `.razor.css` for this file — it's pure composition of `PixelCard`/`PixelMonthCalendar`, which already carry their own styling; this component introduces no new visual rules of its own.

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build src/DzhusShelter.UI/DzhusShelter.UI.csproj`
Expected: 0 errors. Nothing references this component yet (Task 3 wires it in), so this only proves the component itself is well-formed.

- [ ] **Step 3: Commit**

```bash
git add src/DzhusShelter.UI/Components/Shared/BadHabitsMonthSummaryCard.razor
git commit -m "feat(ui): add BadHabitsMonthSummaryCard for the Home dashboard"
```

---

### Task 3: Rewrite Home.razor as the 3-category dashboard

**Files:**
- Modify: `src/DzhusShelter.UI/Components/Pages/Home.razor` (full rewrite)

**Interfaces:**
- Consumes: `<BadHabitsMonthSummaryCard />` (Task 2), `<PixelCard Title>` (existing, unchanged), `wwwroot/hero.webp` (already committed, not part of this plan).

- [ ] **Step 1: Rewrite the page**

Replace `src/DzhusShelter.UI/Components/Pages/Home.razor` in full:

```razor
@page "/"

<PageTitle>Dzhus Shelter</PageTitle>

<div class="hero-container">
    <pre class="ascii-banner">
 ____ ______   _ _   _ ____  
|  _ \__  / | | | | | / ___| 
| | | |/ /| |_| | | | \___ \ 
| |_| / /_|  _  | |_| |___) |
|____/____|_| |_|\___/|____/ 

 ____  _   _ _____ _   _____ _____ ____  
/ ___|| | | | ____| | |_   _| ____|  _ \ 
\___ \| |_| |  _| | |   | | |  _| | |_) |
 ___) |  _  | |___| |___| | | |___|  _ < 
|____/|_| |_|_____|_____|_| |_____|_| \_\
    </pre>
    <img src="hero.webp" class="hero-sprite" alt="Dzhus" />
</div>

<div class="dashboard-category">
    <h2 class="dashboard-category-title">Здоров'я</h2>
    <div class="dashboard-card-grid">
        <BadHabitsMonthSummaryCard />
        <PixelCard Title="🏋️ Спортзал">
            <p>Скоро тут буде статистика.</p>
        </PixelCard>
        <PixelCard Title="📱 Екранний час">
            <p>Скоро тут буде статистика.</p>
        </PixelCard>
    </div>
</div>

<div class="dashboard-category">
    <h2 class="dashboard-category-title">Гроші</h2>
    <div class="dashboard-card-grid">
        <PixelCard Title="💰 Фінанси">
            <p>Скоро тут буде статистика.</p>
        </PixelCard>
        <PixelCard Title="💱 Курс валют">
            <p>Скоро тут буде статистика.</p>
        </PixelCard>
    </div>
</div>

<div class="dashboard-category">
    <h2 class="dashboard-category-title">Інше</h2>
    <div class="dashboard-card-grid">
        <PixelCard Title="🌤️ Погода">
            <p>Скоро тут буде статистика.</p>
        </PixelCard>
        <PixelCard Title="📅 Розклад">
            <p>Скоро тут буде статистика.</p>
        </PixelCard>
        <PixelCard Title="🤖 Боти">
            <p>Скоро тут буде статистика.</p>
        </PixelCard>
    </div>
</div>

<style>
    .hero-container {
        display: flex;
        flex-direction: column;
        align-items: center;
        margin-top: 1rem;
        margin-bottom: 2rem;
    }

    .ascii-banner {
        color: #ffffff;
        text-shadow: 2px 2px 0px #000, 3px 3px 0px #209cee;
        font-family: 'Courier New', Courier, monospace;
        font-size: 14px;
        line-height: 1.1;
        margin: 0 0 8px 0;
        text-align: center;
    }

    .hero-sprite {
        image-rendering: pixelated;
        width: 260px;
    }

    .dashboard-category {
        margin-top: 2rem;
    }

    .dashboard-category-title {
        font-size: 1.25rem;
        margin-bottom: 1rem;
    }

    .dashboard-card-grid {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
        gap: 16px;
    }

    @@media (max-width: 768px) {
        .ascii-banner { font-size: 8px; }
        .hero-sprite { width: 180px; }
    }
</style>
```

Note the `@@media` (double `@`) — this is Razor's escape for a literal `@` inside markup, matching the convention the old `Home.razor` already used for its own `@@keyframes`/`@@media` rules.

- [ ] **Step 2: Build the solution**

Run: `dotnet build DzhusShelter.slnx --configuration Release`
Expected: 0 errors, 0 warnings.

- [ ] **Step 3: Manual browser verification**

Start Postgres (`docker compose up -d postgres`, local `.env` with `POSTGRES_PASSWORD` matching `src/DzhusShelter.Api/appsettings.json`'s default) and the Api and UI locally (check `src/DzhusShelter.UI/appsettings.Development.json`'s `Api:BaseUrl` matches whatever port the Api actually starts on). Seed a couple of Bad Habits entries for the current month via `POST /api/bad-habits/entries` if none already exist, then open `/` and confirm:

1. The ASCII "DZHUS SHELTER" banner renders above the animated sprite; the sprite plays its loop (coding/car-repair/hiking/tinkering scenes) with a transparent background (no black or white box around the character).
2. Three category headings appear in order: Здоров'я, Гроші, Інше.
3. Здоров'я's first card ("Погані звички") shows a read-only calendar for the current month with icons on the days you seeded, no filter dropdown, no month-navigation arrows, no stats list.
4. All other 7 cards show their emoji + title and the exact line "Скоро тут буде статистика." — nothing else.
5. The card grid re-flows to fewer columns on a narrow viewport (resize the browser pane or check via the emulated mobile preset) without any card overflowing horizontally.
6. No unhandled exceptions in the browser console or server logs.

- [ ] **Step 4: Tear down local test resources**

```bash
docker compose down postgres
rm -f .env
```

- [ ] **Step 5: Commit and push**

```bash
git add src/DzhusShelter.UI/Components/Pages/Home.razor
git commit -m "feat(ui): rebuild Home as a 3-category read-only dashboard"
git push
```
