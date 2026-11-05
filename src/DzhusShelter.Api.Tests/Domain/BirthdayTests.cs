using DzhusShelter.Api.Domain.Birthdays;
using FluentAssertions;

namespace DzhusShelter.Api.Tests.Domain;

public class BirthdayTests
{
    private static readonly DateOnly Today = new(2026, 11, 5);
    private static readonly DateTimeOffset CreatedAt = new(2026, 11, 5, 10, 0, 0, TimeSpan.Zero);

    private static Birthday CreateValid(int day, int month, int? year, string name = "Саша Пупкін") =>
        Birthday.Create(name, day, month, year, Today, CreatedAt).Value;

    [Fact]
    public void Create_TrimsName()
    {
        var result = Birthday.Create("  Саша Пупкін ", 12, 11, 2001, Today, CreatedAt);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Саша Пупкін");
        result.Value.Day.Should().Be(12);
        result.Value.Month.Should().Be(11);
        result.Value.Year.Should().Be(2001);
        result.Value.CreatedAt.Should().Be(CreatedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyOrWhitespaceName_Fails(string name)
    {
        var result = Birthday.Create(name, 12, 11, null, Today, CreatedAt);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BirthdayErrors.NameRequired);
    }

    [Fact]
    public void Create_NameOver100Chars_Fails()
    {
        var result = Birthday.Create(new string('а', 101), 12, 11, null, Today, CreatedAt);

        result.Error.Should().Be(BirthdayErrors.NameTooLong);
    }

    [Fact]
    public void Create_NameExactly100Chars_Succeeds()
    {
        Birthday.Create(new string('а', 100), 12, 11, null, Today, CreatedAt).IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(31, 4)]
    [InlineData(30, 2)]
    [InlineData(0, 1)]
    [InlineData(1, 13)]
    [InlineData(1, 0)]
    public void Create_InvalidDayMonth_Fails(int day, int month)
    {
        var result = Birthday.Create("Саша", day, month, null, Today, CreatedAt);

        result.Error.Should().Be(BirthdayErrors.InvalidDate);
    }

    [Fact]
    public void Create_Feb29WithoutYear_Succeeds()
    {
        Birthday.Create("Саша", 29, 2, null, Today, CreatedAt).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Create_Feb29InNonLeapYear_Fails()
    {
        Birthday.Create("Саша", 29, 2, 2001, Today, CreatedAt).Error.Should().Be(BirthdayErrors.InvalidDate);
    }

    [Fact]
    public void Create_Feb29InLeapYear_Succeeds()
    {
        Birthday.Create("Саша", 29, 2, 2000, Today, CreatedAt).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Create_YearBefore1900_Fails()
    {
        Birthday.Create("Саша", 1, 1, 1899, Today, CreatedAt).Error.Should().Be(BirthdayErrors.YearTooEarly);
    }

    [Fact]
    public void Create_FullDateInFuture_Fails()
    {
        Birthday.Create("Саша", 6, 11, 2026, Today, CreatedAt).Error.Should().Be(BirthdayErrors.DateInFuture);
    }

    [Fact]
    public void Create_BornToday_Succeeds()
    {
        Birthday.Create("Саша", 5, 11, 2026, Today, CreatedAt).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Update_ChangesFields()
    {
        var birthday = CreateValid(6, 11, null, "Оля");

        var result = birthday.Update(" Оля Коваль ", 12, 11, 2001, Today);

        result.IsSuccess.Should().BeTrue();
        birthday.Name.Should().Be("Оля Коваль");
        birthday.Day.Should().Be(12);
        birthday.Month.Should().Be(11);
        birthday.Year.Should().Be(2001);
    }

    [Fact]
    public void Update_Invalid_FailsAndLeavesFieldsUnchanged()
    {
        var birthday = CreateValid(6, 11, null, "Оля");

        var result = birthday.Update("Оля", 31, 4, null, Today);

        result.Error.Should().Be(BirthdayErrors.InvalidDate);
        birthday.Day.Should().Be(6);
        birthday.Month.Should().Be(11);
        birthday.Year.Should().BeNull();
    }

    [Fact]
    public void NextOccurrence_TodayIsBirthday_ReturnsToday()
    {
        CreateValid(5, 11, null).NextOccurrence(Today).Should().Be(new DateOnly(2026, 11, 5));
    }

    [Fact]
    public void NextOccurrence_LaterThisYear()
    {
        CreateValid(12, 11, null).NextOccurrence(Today).Should().Be(new DateOnly(2026, 11, 12));
    }

    [Fact]
    public void NextOccurrence_AlreadyPassed_NextYear()
    {
        CreateValid(4, 11, null).NextOccurrence(Today).Should().Be(new DateOnly(2027, 11, 4));
    }

    [Fact]
    public void NextOccurrence_Dec31_TomorrowIsJan1()
    {
        CreateValid(1, 1, null).NextOccurrence(new DateOnly(2026, 12, 31)).Should().Be(new DateOnly(2027, 1, 1));
    }

    [Fact]
    public void NextOccurrence_Feb29InNonLeapYear_IsFeb28()
    {
        var birthday = CreateValid(29, 2, null);

        birthday.NextOccurrence(new DateOnly(2027, 2, 1)).Should().Be(new DateOnly(2027, 2, 28));
        birthday.NextOccurrence(new DateOnly(2028, 2, 1)).Should().Be(new DateOnly(2028, 2, 29));
    }

    [Fact]
    public void NextOccurrence_Feb29AfterFeb28InNonLeapYear_GoesToNextYear()
    {
        CreateValid(29, 2, null).NextOccurrence(new DateOnly(2027, 3, 1)).Should().Be(new DateOnly(2028, 2, 29));
    }

    [Fact]
    public void AgeOn_WithYear()
    {
        CreateValid(12, 11, 2001).AgeOn(new DateOnly(2026, 11, 12)).Should().Be(25);
    }

    [Fact]
    public void AgeOn_WithoutYear_IsNull()
    {
        CreateValid(12, 11, null).AgeOn(new DateOnly(2026, 11, 12)).Should().BeNull();
    }

    [Fact]
    public void BirthdayNotification_Create_SetsFields()
    {
        var sentAt = new DateTimeOffset(2026, 11, 12, 7, 5, 0, TimeSpan.Zero);

        var notification = BirthdayNotification.Create(BirthdayNotificationKind.Today, new DateOnly(2026, 11, 12), sentAt);

        notification.Id.Should().NotBeEmpty();
        notification.Kind.Should().Be(BirthdayNotificationKind.Today);
        notification.ForDate.Should().Be(new DateOnly(2026, 11, 12));
        notification.SentAt.Should().Be(sentAt);
    }
}
