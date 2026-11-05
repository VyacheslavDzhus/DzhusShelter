using DzhusShelter.Api.Application.Birthdays;
using DzhusShelter.Api.Application.Birthdays.Queries;
using DzhusShelter.Api.Domain.Birthdays;
using FluentAssertions;
using NSubstitute;

namespace DzhusShelter.Api.Tests.Application.Birthdays;

public class GetBirthdaysQueryHandlerTests
{
    private static readonly DateOnly Today = new(2026, 11, 5);
    private readonly StubBirthdayClock _clock = new(Today);
    private readonly IBirthdayRepository _repository = Substitute.For<IBirthdayRepository>();

    private Birthday B(string name, int day, int month, int? year) =>
        Birthday.Create(name, day, month, year, Today, _clock.UtcNow).Value;

    [Fact]
    public async Task GetBirthdays_SortedByNextOccurrence_WithDaysUntilAndTurningAge()
    {
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Birthday>
        {
            B("Саша", 12, 11, 2001),
            B("Оля", 6, 11, null),
            B("Іван", 4, 11, 1990),
            B("Борис", 5, 11, null),
        });
        var handler = new GetBirthdaysQueryHandler(_repository, _clock);

        var result = await handler.Handle(new GetBirthdaysQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Select(b => (b.Name, b.DaysUntil, b.TurningAge)).Should().Equal(
            ("Борис", 0, (int?)null),
            ("Оля", 1, (int?)null),
            ("Саша", 7, (int?)25),
            ("Іван", 364, (int?)37));
        result.Value[3].NextOccurrence.Should().Be(new DateOnly(2027, 11, 4));
        result.Value[2].Day.Should().Be(12);
        result.Value[2].Month.Should().Be(11);
        result.Value[2].Year.Should().Be(2001);
    }

    [Fact]
    public async Task GetBirthdays_SameDay_TieBreaksByName()
    {
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Birthday>
        {
            B("Яна", 12, 11, null),
            B("Андрій", 12, 11, null),
        });
        var handler = new GetBirthdaysQueryHandler(_repository, _clock);

        var result = await handler.Handle(new GetBirthdaysQuery(), CancellationToken.None);

        result.Value.Select(b => b.Name).Should().Equal("Андрій", "Яна");
    }
}
