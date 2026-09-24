# Bad Habits: Per-Type Calendar + Stats Cards Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace `BadHabits.razor`'s single combined `PixelChart` with two independent `PixelHabitCard` instances (Alcohol, Smoking), each showing a subtype filter, a month calendar with pixel-art marked days, and a subtype-count stats summary over a selectable period.

**Architecture:** Pure `DzhusShelter.UI` change — no Api or Bot changes. Three new components layered bottom-up: `HabitSubTypeCatalog` (static data: subtype lists, Ukrainian labels, pixel-art icons) → `PixelMonthCalendar` (dumb, reusable calendar-grid renderer) → `PixelHabitCard` (stateful: fetches entries via the existing `BadHabitsApiClient.GetEntriesAsync`, aggregates client-side, wires the other two together). `BadHabits.razor` becomes a thin host for two `PixelHabitCard` instances.

**Tech Stack:** .NET 10, Blazor Server (interactive server render mode), NES.css, existing `BadHabitsApiClient`/`HabitEntryDto` (no changes).

**Spec:** [docs/specs/bad-habits.md](../../specs/bad-habits.md)

## Global Constraints

- No `DzhusShelter.Api` or `DzhusShelter.TelegramBot` changes at all in this plan — every task touches only `src/DzhusShelter.UI/`.
- One subtype filter per card drives both the calendar and the stats list — never two independent filters.
- "Calendar day" for grouping entries is the UTC date of `OccurredAt` (`DateOnly.FromDateTime(e.OccurredAt.UtcDateTime.Date)`) — matches the dedup rule's day boundary already established for this feature.
- When the subtype filter is "Всі" and a day has entries for more than one distinct subtype, the marked-day icon is the lowest-`HabitSubType`-enum-value one's icon plus a small "+" badge. When a specific subtype is selected, the API call itself is already filtered to that subtype, so this multi-icon case cannot arise.
- Stats period presets: this month, 3 months (rolling, ending now), 1 year (rolling, ending now), or a custom `from`/`to` date range. Independent of whatever month the calendar happens to be showing.
- No new `DzhusShelter.UI` test project — this codebase has no automated tests for any existing Blazor component (`PixelCard`, `PixelChart`, `BadHabitsApiClient` in the UI project are all untested today); verification for this plan is build success plus one manual browser pass at the end, consistent with how `PixelChart` was verified when it was built.
- Follow existing conventions already in this codebase: NES.css classes (`nes-select`, `nes-input`, `nes-btn`), the `@bind`/`@bind:after` + `Enum.TryParse` pattern `BadHabits.razor` already uses for its `HabitType` dropdown, and `PixelCard`/`PixelChart`'s scoped-`.razor.css` styling approach (hardcoded hex colors, no shared CSS variable system — see `PixelChart.razor.css` for the precedent).

---

### Task 1: HabitSubTypeCatalog — subtypes, labels, and pixel-art icons

**Files:**
- Create: `src/DzhusShelter.UI/Components/Shared/HabitSubTypeCatalog.cs`

**Interfaces:**
- Produces: `HabitSubTypeCatalog.SubTypesFor(HabitType) : IReadOnlyList<HabitSubType>`, `HabitSubTypeCatalog.DisplayName(HabitSubType) : string`, `HabitSubTypeCatalog.IconSvg(HabitSubType) : string` (raw `<svg>...</svg>` markup, NOT wrapped in `MarkupString` — callers wrap it themselves at the point they compose a larger markup string, so this stays a plain, easily-concatenated `string`). Task 2 and Task 3 depend on all three methods.

- [ ] **Step 1: Create the catalog**

```csharp
using DzhusShelter.UI.Services;

namespace DzhusShelter.UI.Components.Shared;

public static class HabitSubTypeCatalog
{
    private static readonly Dictionary<HabitType, HabitSubType[]> SubTypesByHabitType = new()
    {
        [HabitType.Smoking] = [HabitSubType.Cigarette, HabitSubType.Vape, HabitSubType.Iqos, HabitSubType.Hookah],
        [HabitType.Alcohol] = [HabitSubType.Beer, HabitSubType.Wine, HabitSubType.Vodka, HabitSubType.Whiskey, HabitSubType.Rum, HabitSubType.Gin, HabitSubType.Martini],
    };

    private static readonly Dictionary<HabitSubType, string> DisplayNames = new()
    {
        [HabitSubType.Cigarette] = "Цигарки",
        [HabitSubType.Vape] = "Вейп",
        [HabitSubType.Iqos] = "Айкос",
        [HabitSubType.Hookah] = "Кальян",
        [HabitSubType.Beer] = "Пиво",
        [HabitSubType.Wine] = "Вино",
        [HabitSubType.Vodka] = "Горілка",
        [HabitSubType.Whiskey] = "Віскі",
        [HabitSubType.Rum] = "Ром",
        [HabitSubType.Gin] = "Джин",
        [HabitSubType.Martini] = "Мартіні",
    };

    // Every icon shares a 16x16 viewBox so callers can size them uniformly.
    // Flat rects only (no strokes/gradients) for a consistent chunky pixel-art look.
    private static readonly Dictionary<HabitSubType, string> Icons = new()
    {
        [HabitSubType.Cigarette] =
            "<svg viewBox=\"0 0 16 16\" width=\"14\" height=\"14\">" +
            "<rect x=\"1\" y=\"7\" width=\"1\" height=\"2\" fill=\"#cccccc\"/>" +
            "<rect x=\"2\" y=\"7\" width=\"10\" height=\"2\" fill=\"#ffffff\"/>" +
            "<rect x=\"12\" y=\"7\" width=\"2\" height=\"2\" fill=\"#ff5533\"/>" +
            "<rect x=\"13\" y=\"3\" width=\"1\" height=\"1\" fill=\"#dddddd\"/>" +
            "<rect x=\"14\" y=\"2\" width=\"1\" height=\"1\" fill=\"#dddddd\"/>" +
            "</svg>",
        [HabitSubType.Vape] =
            "<svg viewBox=\"0 0 16 16\" width=\"14\" height=\"14\">" +
            "<rect x=\"6\" y=\"3\" width=\"4\" height=\"10\" fill=\"#4a6b8a\"/>" +
            "<rect x=\"7\" y=\"1\" width=\"2\" height=\"2\" fill=\"#333333\"/>" +
            "<rect x=\"7\" y=\"11\" width=\"2\" height=\"1\" fill=\"#ffaa33\"/>" +
            "</svg>",
        [HabitSubType.Iqos] =
            "<svg viewBox=\"0 0 16 16\" width=\"14\" height=\"14\">" +
            "<rect x=\"6\" y=\"2\" width=\"4\" height=\"12\" fill=\"#333333\"/>" +
            "<rect x=\"6\" y=\"9\" width=\"4\" height=\"2\" fill=\"#c0c0c0\"/>" +
            "</svg>",
        [HabitSubType.Hookah] =
            "<svg viewBox=\"0 0 16 16\" width=\"14\" height=\"14\">" +
            "<rect x=\"6\" y=\"1\" width=\"4\" height=\"2\" fill=\"#333333\"/>" +
            "<rect x=\"7\" y=\"3\" width=\"2\" height=\"6\" fill=\"#c0c0c0\"/>" +
            "<rect x=\"6\" y=\"8\" width=\"4\" height=\"1\" fill=\"#1f8080\"/>" +
            "<rect x=\"5\" y=\"9\" width=\"6\" height=\"5\" fill=\"#1f8080\"/>" +
            "</svg>",
        [HabitSubType.Beer] =
            "<svg viewBox=\"0 0 16 16\" width=\"14\" height=\"14\">" +
            "<rect x=\"4\" y=\"3\" width=\"7\" height=\"2\" fill=\"#ffffff\"/>" +
            "<rect x=\"4\" y=\"5\" width=\"7\" height=\"9\" fill=\"#f2a900\"/>" +
            "<rect x=\"11\" y=\"6\" width=\"2\" height=\"6\" fill=\"#f2a900\"/>" +
            "</svg>",
        [HabitSubType.Wine] =
            "<svg viewBox=\"0 0 16 16\" width=\"14\" height=\"14\">" +
            "<rect x=\"5\" y=\"3\" width=\"6\" height=\"5\" fill=\"#7b1030\"/>" +
            "<rect x=\"7\" y=\"8\" width=\"2\" height=\"4\" fill=\"#cfcfcf\"/>" +
            "<rect x=\"5\" y=\"12\" width=\"6\" height=\"2\" fill=\"#cfcfcf\"/>" +
            "</svg>",
        [HabitSubType.Vodka] =
            "<svg viewBox=\"0 0 16 16\" width=\"14\" height=\"14\">" +
            "<rect x=\"6\" y=\"1\" width=\"4\" height=\"2\" fill=\"#000000\"/>" +
            "<rect x=\"7\" y=\"3\" width=\"2\" height=\"3\" fill=\"#bfe6ff\"/>" +
            "<rect x=\"5\" y=\"6\" width=\"6\" height=\"8\" fill=\"#bfe6ff\"/>" +
            "</svg>",
        [HabitSubType.Whiskey] =
            "<svg viewBox=\"0 0 16 16\" width=\"14\" height=\"14\">" +
            "<rect x=\"5\" y=\"5\" width=\"6\" height=\"3\" fill=\"#cccccc\"/>" +
            "<rect x=\"5\" y=\"8\" width=\"6\" height=\"6\" fill=\"#b06a1e\"/>" +
            "</svg>",
        [HabitSubType.Rum] =
            "<svg viewBox=\"0 0 16 16\" width=\"14\" height=\"14\">" +
            "<rect x=\"6\" y=\"1\" width=\"4\" height=\"2\" fill=\"#000000\"/>" +
            "<rect x=\"7\" y=\"3\" width=\"2\" height=\"3\" fill=\"#4a2a10\"/>" +
            "<rect x=\"5\" y=\"6\" width=\"6\" height=\"8\" fill=\"#4a2a10\"/>" +
            "</svg>",
        [HabitSubType.Gin] =
            "<svg viewBox=\"0 0 16 16\" width=\"14\" height=\"14\">" +
            "<rect x=\"6\" y=\"1\" width=\"4\" height=\"2\" fill=\"#000000\"/>" +
            "<rect x=\"7\" y=\"3\" width=\"2\" height=\"3\" fill=\"#2f6e3f\"/>" +
            "<rect x=\"5\" y=\"6\" width=\"6\" height=\"8\" fill=\"#2f6e3f\"/>" +
            "</svg>",
        [HabitSubType.Martini] =
            "<svg viewBox=\"0 0 16 16\" width=\"14\" height=\"14\">" +
            "<rect x=\"3\" y=\"3\" width=\"10\" height=\"2\" fill=\"#d8d8d8\"/>" +
            "<rect x=\"5\" y=\"5\" width=\"6\" height=\"2\" fill=\"#d8d8d8\"/>" +
            "<rect x=\"7\" y=\"5\" width=\"2\" height=\"2\" fill=\"#4a7c2f\"/>" +
            "<rect x=\"7\" y=\"7\" width=\"2\" height=\"2\" fill=\"#d8d8d8\"/>" +
            "<rect x=\"7\" y=\"9\" width=\"2\" height=\"4\" fill=\"#d8d8d8\"/>" +
            "<rect x=\"5\" y=\"13\" width=\"6\" height=\"1\" fill=\"#d8d8d8\"/>" +
            "</svg>",
    };

    public static IReadOnlyList<HabitSubType> SubTypesFor(HabitType habitType) => SubTypesByHabitType[habitType];

    public static string DisplayName(HabitSubType subType) => DisplayNames[subType];

    public static string IconSvg(HabitSubType subType) => Icons[subType];
}
```

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build src/DzhusShelter.UI/DzhusShelter.UI.csproj`
Expected: 0 errors. (No automated test for this file — see Global Constraints. Self-review that all 11 `HabitSubType` values from `src/DzhusShelter.UI/Services/BadHabitsApiClient.cs` have an entry in all three dictionaries — a missing one throws `KeyNotFoundException` at first use.)

- [ ] **Step 3: Commit**

```bash
git add src/DzhusShelter.UI/Components/Shared/HabitSubTypeCatalog.cs
git commit -m "feat(ui): add HabitSubTypeCatalog with labels and pixel-art icons"
```

---

### Task 2: PixelMonthCalendar — pure calendar-grid component

**Files:**
- Create: `src/DzhusShelter.UI/Components/Shared/PixelMonthCalendar.razor`
- Create: `src/DzhusShelter.UI/Components/Shared/PixelMonthCalendar.razor.css`

**Interfaces:**
- Consumes: nothing from Task 1 directly (it only knows about `MarkupString`, not `HabitSubTypeCatalog`).
- Produces: `<PixelMonthCalendar Year="int" Month="int" MarkedDays="IReadOnlyDictionary<DateOnly, MarkupString>" />` — a dumb, reusable renderer with no data fetching of its own. Task 3 depends on this exact parameter shape.

- [ ] **Step 1: Create the component**

```razor
<div class="pixel-calendar">
    <div class="pixel-calendar-weekday-row">
        @foreach (var day in WeekdayHeaders)
        {
            <div class="pixel-calendar-weekday">@day</div>
        }
    </div>
    <div class="pixel-calendar-grid">
        @foreach (var cell in BuildCells())
        {
            @if (cell is null)
            {
                <div class="pixel-calendar-cell pixel-calendar-cell-blank"></div>
            }
            else
            {
                var (day, date) = cell.Value;
                <div class="pixel-calendar-cell @(MarkedDays.ContainsKey(date) ? "pixel-calendar-cell-marked" : "")">
                    <span class="pixel-calendar-day-number">@day</span>
                    @if (MarkedDays.TryGetValue(date, out var marker))
                    {
                        @marker
                    }
                </div>
            }
        }
    </div>
</div>

@code {
    private static readonly string[] WeekdayHeaders = ["Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Нд"];

    [Parameter, EditorRequired] public int Year { get; set; }
    [Parameter, EditorRequired] public int Month { get; set; }
    [Parameter] public IReadOnlyDictionary<DateOnly, MarkupString> MarkedDays { get; set; } = new Dictionary<DateOnly, MarkupString>();

    private List<(int Day, DateOnly Date)?> BuildCells()
    {
        var daysInMonth = DateTime.DaysInMonth(Year, Month);
        var firstDay = new DateOnly(Year, Month, 1);
        // DayOfWeek: Sunday = 0 .. Saturday = 6. Convert to Monday-first index (0 = Monday .. 6 = Sunday).
        var leadingBlanks = ((int)firstDay.DayOfWeek + 6) % 7;

        var cells = new List<(int, DateOnly)?>();
        for (var i = 0; i < leadingBlanks; i++)
            cells.Add(null);

        for (var day = 1; day <= daysInMonth; day++)
            cells.Add((day, new DateOnly(Year, Month, day)));

        var trailingBlanks = (7 - cells.Count % 7) % 7;
        for (var i = 0; i < trailingBlanks; i++)
            cells.Add(null);

        return cells;
    }
}
```

- [ ] **Step 2: Add scoped styles**

```css
.pixel-calendar {
    color: #ffffff;
}

.pixel-calendar-weekday-row,
.pixel-calendar-grid {
    display: grid;
    grid-template-columns: repeat(7, 1fr);
    gap: 2px;
}

.pixel-calendar-weekday {
    text-align: center;
    font-size: 10px;
    color: #cccccc;
    padding: 2px 0;
}

.pixel-calendar-cell {
    position: relative;
    aspect-ratio: 1 / 1;
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    background-color: #1a1a1c;
    border: 1px solid #38383b;
}

.pixel-calendar-cell-blank {
    background-color: transparent;
    border: none;
}

.pixel-calendar-cell-marked {
    background-color: #38383b;
    border: 1px solid #5e5e62;
}

.pixel-calendar-day-number {
    font-size: 9px;
    line-height: 1;
}

.pixel-calendar-marker {
    position: relative;
    display: flex;
    align-items: center;
    justify-content: center;
}

.pixel-calendar-plus {
    position: absolute;
    bottom: -2px;
    right: -2px;
    font-size: 8px;
    color: #ffcc00;
    text-shadow: 1px 1px 0px #000;
}
```

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build src/DzhusShelter.UI/DzhusShelter.UI.csproj`
Expected: 0 errors. (Visual verification happens in Task 5, once this is actually wired into a page — building in isolation only proves it compiles.)

- [ ] **Step 4: Commit**

```bash
git add src/DzhusShelter.UI/Components/Shared/PixelMonthCalendar.razor src/DzhusShelter.UI/Components/Shared/PixelMonthCalendar.razor.css
git commit -m "feat(ui): add reusable PixelMonthCalendar grid component"
```

---

### Task 3: PixelHabitCard — subtype filter, month navigation, and calendar wiring

**Files:**
- Create: `src/DzhusShelter.UI/Components/Shared/PixelHabitCard.razor`
- Create: `src/DzhusShelter.UI/Components/Shared/PixelHabitCard.razor.css`

**Interfaces:**
- Consumes: `HabitSubTypeCatalog.SubTypesFor/DisplayName/IconSvg` (Task 1), `<PixelMonthCalendar Year Month MarkedDays>` (Task 2), `BadHabitsApiClient.GetEntriesAsync(DateTimeOffset, DateTimeOffset, HabitType?, HabitSubType?, CancellationToken)` (existing, `src/DzhusShelter.UI/Services/BadHabitsApiClient.cs`), `PixelCard` (existing, `src/DzhusShelter.UI/Components/Shared/PixelCard.razor`).
- Produces: `<PixelHabitCard HabitType="HabitType" Title="string" />`. This task builds the filter + calendar half only — no stats section yet (Task 4 adds it to the same file). `ReloadAsync` in this task fetches only the displayed month's range; Task 4 will widen it.

- [ ] **Step 1: Create the component**

```razor
@using DzhusShelter.UI.Services
@inject BadHabitsApiClient ApiClient

<PixelCard Title="@Title" Class="pixel-habit-card">
    <div class="pixel-habit-card-filter">
        <label>
            Підтип:
            <select class="nes-select" @bind="_selectedSubTypeRaw" @bind:after="ReloadAsync">
                <option value="">Всі</option>
                @foreach (var subType in HabitSubTypeCatalog.SubTypesFor(HabitType))
                {
                    <option value="@subType">@HabitSubTypeCatalog.DisplayName(subType)</option>
                }
            </select>
        </label>
    </div>

    <div class="pixel-habit-card-month-nav">
        <button type="button" class="nes-btn" @onclick="GoToPreviousMonth">◀</button>
        <span class="pixel-habit-card-month-label">@MonthNames[_viewMonth - 1] @_viewYear</span>
        <button type="button" class="nes-btn" @onclick="GoToNextMonth">▶</button>
    </div>

    @if (_entries is null)
    {
        <p><em>Завантаження...</em></p>
    }
    else
    {
        <PixelMonthCalendar Year="_viewYear" Month="_viewMonth" MarkedDays="_markedDays" />
    }
</PixelCard>

@code {
    private static readonly string[] MonthNames =
    [
        "Січень", "Лютий", "Березень", "Квітень", "Травень", "Червень",
        "Липень", "Серпень", "Вересень", "Жовтень", "Листопад", "Грудень",
    ];

    [Parameter, EditorRequired] public HabitType HabitType { get; set; }
    [Parameter, EditorRequired] public string Title { get; set; } = string.Empty;

    private string _selectedSubTypeRaw = "";
    private int _viewYear = DateTime.UtcNow.Year;
    private int _viewMonth = DateTime.UtcNow.Month;
    private List<HabitEntryDto>? _entries;
    private Dictionary<DateOnly, MarkupString> _markedDays = new();

    private HabitSubType? SelectedSubType =>
        Enum.TryParse<HabitSubType>(_selectedSubTypeRaw, out var parsed) ? parsed : null;

    protected override async Task OnInitializedAsync() => await ReloadAsync();

    private async Task GoToPreviousMonth()
    {
        _viewMonth--;
        if (_viewMonth < 1)
        {
            _viewMonth = 12;
            _viewYear--;
        }
        await ReloadAsync();
    }

    private async Task GoToNextMonth()
    {
        _viewMonth++;
        if (_viewMonth > 12)
        {
            _viewMonth = 1;
            _viewYear++;
        }
        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        var monthStart = new DateTimeOffset(new DateTime(_viewYear, _viewMonth, 1), TimeSpan.Zero);
        var monthEnd = monthStart.AddMonths(1).AddTicks(-1);

        _entries = await ApiClient.GetEntriesAsync(monthStart, monthEnd, HabitType, SelectedSubType, CancellationToken.None);
        _markedDays = BuildMarkedDays(_entries);
    }

    private static Dictionary<DateOnly, MarkupString> BuildMarkedDays(IEnumerable<HabitEntryDto> entries)
    {
        var result = new Dictionary<DateOnly, MarkupString>();
        foreach (var group in entries.GroupBy(e => DateOnly.FromDateTime(e.OccurredAt.UtcDateTime.Date)))
        {
            var distinctSubTypes = group.Select(e => e.SubType).Distinct().OrderBy(s => (int)s).ToList();
            var primaryIcon = HabitSubTypeCatalog.IconSvg(distinctSubTypes[0]);
            var markup = distinctSubTypes.Count > 1
                ? $"<div class=\"pixel-calendar-marker\">{primaryIcon}<span class=\"pixel-calendar-plus\">+</span></div>"
                : $"<div class=\"pixel-calendar-marker\">{primaryIcon}</div>";
            result[group.Key] = new MarkupString(markup);
        }
        return result;
    }
}
```

- [ ] **Step 2: Add scoped styles**

```css
.pixel-habit-card-filter {
    margin-bottom: 12px;
}

.pixel-habit-card-month-nav {
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 12px;
    margin-bottom: 12px;
}

.pixel-habit-card-month-label {
    font-size: 12px;
    min-width: 140px;
    text-align: center;
}
```

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build src/DzhusShelter.UI/DzhusShelter.UI.csproj`
Expected: 0 errors. Nothing references `PixelHabitCard` yet (Task 5 wires it into `BadHabits.razor`), so this only proves the component itself is well-formed — full behavioral verification happens in Task 5.

- [ ] **Step 4: Commit**

```bash
git add src/DzhusShelter.UI/Components/Shared/PixelHabitCard.razor src/DzhusShelter.UI/Components/Shared/PixelHabitCard.razor.css
git commit -m "feat(ui): add PixelHabitCard with subtype filter and month calendar"
```

---

### Task 4: PixelHabitCard — stats period selector and subtype-count list

**Files:**
- Modify: `src/DzhusShelter.UI/Components/Shared/PixelHabitCard.razor`
- Modify: `src/DzhusShelter.UI/Components/Shared/PixelHabitCard.razor.css`

**Interfaces:**
- Consumes: everything Task 3 already set up on this file (`_selectedSubTypeRaw`/`SelectedSubType`, `_viewYear`/`_viewMonth`, `HabitSubTypeCatalog`, `ApiClient`).
- Produces: a stats period selector (`ThisMonth`/`ThreeMonths`/`Year`/`Custom`) and a sorted `{icon} {label} ×{count}` list beneath the calendar, both driven by the same `SelectedSubType` filter Task 3 built. `ReloadAsync` is widened to fetch the union of the calendar month and the stats period in one API call.

- [ ] **Step 1: Add the stats UI beneath the calendar**

In `src/DzhusShelter.UI/Components/Shared/PixelHabitCard.razor`, add this markup right after the closing `</PixelMonthCalendar>`'s containing `else` block (i.e., as a sibling of the calendar, still inside `<PixelCard>`):

```razor
        <div class="pixel-habit-card-stats-period">
            <label>
                Період:
                <select class="nes-select" @bind="_statsPeriod" @bind:after="ReloadAsync">
                    <option value="@StatsPeriod.ThisMonth">Цей місяць</option>
                    <option value="@StatsPeriod.ThreeMonths">3 місяці</option>
                    <option value="@StatsPeriod.Year">Рік</option>
                    <option value="@StatsPeriod.Custom">Свій період</option>
                </select>
            </label>
            @if (_statsPeriod == StatsPeriod.Custom)
            {
                <label>
                    Від: <input type="date" class="nes-input" @bind="_customFrom" @bind:after="ReloadAsync" />
                </label>
                <label>
                    До: <input type="date" class="nes-input" @bind="_customTo" @bind:after="ReloadAsync" />
                </label>
            }
        </div>

        @if (_stats.Count == 0)
        {
            <p>Немає даних за обраний період.</p>
        }
        else
        {
            <ul class="pixel-habit-card-stats-list">
                @foreach (var stat in _stats)
                {
                    <li>
                        @((MarkupString)HabitSubTypeCatalog.IconSvg(stat.SubType)) @HabitSubTypeCatalog.DisplayName(stat.SubType) ×@stat.Count
                    </li>
                }
            </ul>
        }
```

(This sits inside the `else` branch that currently only renders `<PixelMonthCalendar ... />` — both the calendar and the stats section render together once `_entries` is loaded, so wrap them as siblings inside that same `else { }` block rather than nesting a second `@if (_entries is null)` check.)

- [ ] **Step 2: Add the stats state, period-range computation, and widen ReloadAsync**

In the `@code` block, add:

```csharp
    private enum StatsPeriod
    {
        ThisMonth,
        ThreeMonths,
        Year,
        Custom,
    }

    private StatsPeriod _statsPeriod = StatsPeriod.ThisMonth;
    private DateTime _customFrom = DateTime.UtcNow.Date.AddMonths(-1);
    private DateTime _customTo = DateTime.UtcNow.Date;
    private List<(HabitSubType SubType, int Count)> _stats = [];
```

Replace the existing `ReloadAsync` method with:

```csharp
    private async Task ReloadAsync()
    {
        var monthStart = new DateTimeOffset(new DateTime(_viewYear, _viewMonth, 1), TimeSpan.Zero);
        var monthEnd = monthStart.AddMonths(1).AddTicks(-1);
        var (statsFrom, statsTo) = GetStatsRange();

        var rangeFrom = monthStart < statsFrom ? monthStart : statsFrom;
        var rangeTo = monthEnd > statsTo ? monthEnd : statsTo;

        _entries = await ApiClient.GetEntriesAsync(rangeFrom, rangeTo, HabitType, SelectedSubType, CancellationToken.None);
        _markedDays = BuildMarkedDays(_entries.Where(e => e.OccurredAt >= monthStart && e.OccurredAt <= monthEnd));
        _stats = ComputeStats(_entries, statsFrom, statsTo);
    }

    private (DateTimeOffset From, DateTimeOffset To) GetStatsRange()
    {
        var today = DateTime.UtcNow.Date;
        return _statsPeriod switch
        {
            StatsPeriod.ThisMonth => (new DateTimeOffset(new DateTime(today.Year, today.Month, 1), TimeSpan.Zero), DateTimeOffset.UtcNow),
            StatsPeriod.ThreeMonths => (new DateTimeOffset(today.AddMonths(-3), TimeSpan.Zero), DateTimeOffset.UtcNow),
            StatsPeriod.Year => (new DateTimeOffset(today.AddYears(-1), TimeSpan.Zero), DateTimeOffset.UtcNow),
            StatsPeriod.Custom => (
                new DateTimeOffset(DateTime.SpecifyKind(_customFrom, DateTimeKind.Unspecified), TimeSpan.Zero),
                new DateTimeOffset(DateTime.SpecifyKind(_customTo, DateTimeKind.Unspecified).AddDays(1).AddTicks(-1), TimeSpan.Zero)),
            _ => throw new ArgumentOutOfRangeException(),
        };
    }

    private static List<(HabitSubType SubType, int Count)> ComputeStats(IEnumerable<HabitEntryDto> entries, DateTimeOffset from, DateTimeOffset to) =>
        entries
            .Where(e => e.OccurredAt >= from && e.OccurredAt <= to)
            .GroupBy(e => e.SubType)
            .Select(g => (SubType: g.Key, Count: g.Count()))
            .OrderByDescending(s => s.Count)
            .ToList();
```

Note: `_markedDays` is now built from only the entries within the displayed calendar month (`BuildMarkedDays` is called with a filtered `.Where(...)` over `_entries`, since `_entries` may now span a wider range than just the visible month) — the calendar must never show a marker for a day outside the month it's displaying.

- [ ] **Step 3: Add stats-section styles**

Append to `src/DzhusShelter.UI/Components/Shared/PixelHabitCard.razor.css`:

```css
.pixel-habit-card-stats-period {
    display: flex;
    gap: 12px;
    flex-wrap: wrap;
    align-items: center;
    margin: 16px 0 8px;
}

.pixel-habit-card-stats-list {
    list-style: none;
    padding: 0;
    margin: 0;
}

.pixel-habit-card-stats-list li {
    display: flex;
    align-items: center;
    gap: 8px;
    padding: 4px 0;
    font-size: 12px;
}
```

- [ ] **Step 4: Build to verify it compiles**

Run: `dotnet build src/DzhusShelter.UI/DzhusShelter.UI.csproj`
Expected: 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/DzhusShelter.UI/Components/Shared/PixelHabitCard.razor src/DzhusShelter.UI/Components/Shared/PixelHabitCard.razor.css
git commit -m "feat(ui): add stats period selector and subtype-count list to PixelHabitCard"
```

---

### Task 5: Wire PixelHabitCard into BadHabits.razor and verify in the browser

**Files:**
- Modify: `src/DzhusShelter.UI/Components/Pages/BadHabits.razor` (full rewrite — the old filter bar + `PixelChart` usage is removed entirely)

**Interfaces:**
- Consumes: `<PixelHabitCard HabitType="HabitType" Title="string" />` (Task 3/4).

- [ ] **Step 1: Rewrite the page**

Replace `src/DzhusShelter.UI/Components/Pages/BadHabits.razor` in full:

```razor
@page "/bad-habits"
@rendermode InteractiveServer
@using DzhusShelter.UI.Services

<PageTitle>Bad Habits</PageTitle>

<h1>Bad Habits</h1>

<PixelHabitCard HabitType="HabitType.Alcohol" Title="Алкоголь" />
<PixelHabitCard HabitType="HabitType.Smoking" Title="Куріння" />
```

- [ ] **Step 2: Build the solution**

Run: `dotnet build DzhusShelter.slnx --configuration Release`
Expected: 0 errors, 0 warnings.

- [ ] **Step 3: Manual browser verification**

Start Postgres (`docker compose up -d postgres`, with a local `.env` containing `POSTGRES_PASSWORD` matching `src/DzhusShelter.Api/appsettings.json`'s default), then run the Api and UI locally (`dotnet run --project src/DzhusShelter.Api/DzhusShelter.Api.csproj`, `dotnet run --project src/DzhusShelter.UI/DzhusShelter.UI.csproj` — check `src/DzhusShelter.UI/appsettings.Development.json`'s `Api:BaseUrl` matches whatever port the Api actually started on). Seed a handful of test entries across a few different days/subtypes for both `Alcohol` and `Smoking` via `POST /api/bad-habits/entries` (curl or the Swagger UI), then open `/bad-habits` and confirm:

1. Both cards render side by side (or stacked, depending on viewport width) with their own titles ("Алкоголь", "Куріння").
2. Each card's subtype dropdown only lists that card's own subtypes (Alcohol's 7, Smoking's 4), plus "Всі".
3. Days with a logged entry show a small pixel-art icon matching that entry's subtype; a day with two different alcohol subtypes logged (when filter = "Всі") shows one icon plus a small "+" badge.
4. Selecting a specific subtype in the filter narrows the calendar to only that subtype's days, and the stats list below narrows to just that one subtype's count.
5. ◀ / ▶ navigate months correctly across a year boundary (e.g. December → January).
6. Changing the stats period dropdown (Цей місяць / 3 місяці / Рік / Свій період) changes the counts shown, independent of whatever month the calendar is currently displaying. "Свій період" reveals two date inputs and updates the stats when either changes.
7. No unhandled exceptions in the browser console or server logs during any of the above.

- [ ] **Step 4: Tear down local test resources**

```bash
docker compose down postgres
rm -f .env
```

- [ ] **Step 5: Commit and push**

```bash
git add src/DzhusShelter.UI/Components/Pages/BadHabits.razor
git commit -m "feat(ui): replace single Bad Habits chart with per-type calendar+stats cards"
git push
```
