using DzhusShelter.Api.Application.Birthdays;
using DzhusShelter.Api.Domain.Birthdays;
using DzhusShelter.Api.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace DzhusShelter.Api.IntegrationTests;

public class BirthdayRepositoriesTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory _factory;

    public BirthdayRepositoriesTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
        // Starts the host, which auto-migrates the Testcontainers Postgres.
        _factory.CreateClient().Dispose();
    }

    private static readonly DateOnly Today = new(2026, 11, 5);

    private async Task<T> InScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        using var scope = _factory.Services.CreateScope();
        return await action(scope.ServiceProvider);
    }

    [Fact]
    public async Task BirthdayRepository_AddUpdateDelete_RoundTrips()
    {
        var birthday = Birthday.Create("Оля", 7, 3, null, Today, DateTimeOffset.UtcNow).Value!;

        await InScopeAsync(async sp =>
        {
            await sp.GetRequiredService<IBirthdayRepository>().AddAsync(birthday, default);
            return 0;
        });

        var loaded = await InScopeAsync(sp => sp.GetRequiredService<IBirthdayRepository>().GetByIdAsync(birthday.Id, default));
        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("Оля");
        loaded.Day.Should().Be(7);
        loaded.Month.Should().Be(3);
        loaded.Year.Should().BeNull();

        await InScopeAsync(async sp =>
        {
            var repo = sp.GetRequiredService<IBirthdayRepository>();
            var tracked = await repo.GetByIdAsync(birthday.Id, default);
            tracked!.Update("Ольга", 8, 3, 1990, Today).IsSuccess.Should().BeTrue();
            await repo.SaveChangesAsync(default);
            return 0;
        });

        var updated = await InScopeAsync(sp => sp.GetRequiredService<IBirthdayRepository>().GetByIdAsync(birthday.Id, default));
        updated!.Name.Should().Be("Ольга");
        updated.Day.Should().Be(8);
        updated.Year.Should().Be(1990);

        var all = await InScopeAsync(sp => sp.GetRequiredService<IBirthdayRepository>().GetAllAsync(default));
        all.Should().Contain(b => b.Id == birthday.Id);

        await InScopeAsync(async sp =>
        {
            var repo = sp.GetRequiredService<IBirthdayRepository>();
            await repo.DeleteAsync((await repo.GetByIdAsync(birthday.Id, default))!, default);
            return 0;
        });

        var gone = await InScopeAsync(sp => sp.GetRequiredService<IBirthdayRepository>().GetByIdAsync(birthday.Id, default));
        gone.Should().BeNull();
    }

    [Fact]
    public async Task NotificationRepository_TryAddTwice_SecondReturnsFalse_AndOnlyOneRowExists()
    {
        var forDate = new DateOnly(2031, 1, 10);

        var first = await InScopeAsync(sp => sp.GetRequiredService<IBirthdayNotificationRepository>().TryAddAsync(
            BirthdayNotification.Create(BirthdayNotificationKind.Today, forDate, DateTimeOffset.UtcNow), default));
        var second = await InScopeAsync(sp => sp.GetRequiredService<IBirthdayNotificationRepository>().TryAddAsync(
            BirthdayNotification.Create(BirthdayNotificationKind.Today, forDate, DateTimeOffset.UtcNow), default));

        first.Should().BeTrue();
        second.Should().BeFalse();

        var count = await InScopeAsync(sp => sp.GetRequiredService<AppDbContext>().BirthdayNotifications
            .CountAsync(n => n.Kind == BirthdayNotificationKind.Today && n.ForDate == forDate));
        count.Should().Be(1);
    }

    [Fact]
    public async Task NotificationRepository_ExistsAsync_TrueOnlyForThatKindAndDate()
    {
        var forDate = new DateOnly(2032, 2, 20);
        await InScopeAsync(sp => sp.GetRequiredService<IBirthdayNotificationRepository>().TryAddAsync(
            BirthdayNotification.Create(BirthdayNotificationKind.Today, forDate, DateTimeOffset.UtcNow), default));

        Task<bool> Exists(BirthdayNotificationKind kind, DateOnly date) =>
            InScopeAsync(sp => sp.GetRequiredService<IBirthdayNotificationRepository>().ExistsAsync(kind, date, default));

        (await Exists(BirthdayNotificationKind.Today, forDate)).Should().BeTrue();
        (await Exists(BirthdayNotificationKind.Today, forDate.AddDays(1))).Should().BeFalse();
        (await Exists(BirthdayNotificationKind.Tomorrow, forDate)).Should().BeFalse();
    }
}
