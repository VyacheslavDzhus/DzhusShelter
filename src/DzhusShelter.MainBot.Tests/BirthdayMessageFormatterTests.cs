using DzhusShelter.MainBot.Birthdays;
using FluentAssertions;

namespace DzhusShelter.MainBot.Tests;

public class BirthdayMessageFormatterTests
{
    private static readonly DateOnly Date = new(2026, 11, 5);
    private static readonly BirthdayPerson WithAge = new("Саша Пупкін", 25);
    private static readonly BirthdayPerson NoAge = new("Оля Коваль", null);

    private static string Format(BirthdayNotificationKind kind, params BirthdayPerson[] people) =>
        BirthdayMessageFormatter.Format(new PendingBirthdayNotification(kind, Date, people));

    [Fact]
    public void Today_OnePersonWithAge() =>
        Format(BirthdayNotificationKind.Today, WithAge)
            .Should().Be("🎂 Привіт! Сьогодні у Саша Пупкін день народження — 25 років! 🥳");

    [Fact]
    public void Today_OnePersonNoAge() =>
        Format(BirthdayNotificationKind.Today, NoAge)
            .Should().Be("🎂 Привіт! Сьогодні у Оля Коваль день народження! 🥳");

    [Fact]
    public void Today_TwoPeople() =>
        Format(BirthdayNotificationKind.Today, WithAge, NoAge)
            .Should().Be("🎂 Привіт! Сьогодні день народження у:\n• Саша Пупкін — 25 років 🥳\n• Оля Коваль 🥳");

    [Fact]
    public void Tomorrow_OnePersonWithAge() =>
        Format(BirthdayNotificationKind.Tomorrow, WithAge)
            .Should().Be("⏰ Привіт! Завтра у Саша Пупкін день народження — 25 років 🎁");

    [Fact]
    public void Tomorrow_OnePersonNoAge() =>
        Format(BirthdayNotificationKind.Tomorrow, NoAge)
            .Should().Be("⏰ Привіт! Завтра у Оля Коваль день народження 🎁");

    [Fact]
    public void Tomorrow_TwoPeople() =>
        Format(BirthdayNotificationKind.Tomorrow, WithAge, NoAge)
            .Should().Be("⏰ Привіт! Завтра день народження у:\n• Саша Пупкін — 25 років\n• Оля Коваль\n🎁 Не забудь привітати!");

    [Theory]
    [InlineData(0, "0 років")]
    [InlineData(1, "1 рік")]
    [InlineData(2, "2 роки")]
    [InlineData(4, "4 роки")]
    [InlineData(5, "5 років")]
    [InlineData(11, "11 років")]
    [InlineData(12, "12 років")]
    [InlineData(14, "14 років")]
    [InlineData(21, "21 рік")]
    [InlineData(22, "22 роки")]
    [InlineData(25, "25 років")]
    [InlineData(101, "101 рік")]
    [InlineData(111, "111 років")]
    public void Years_UsesUkrainianPlural(int age, string expected) =>
        BirthdayMessageFormatter.Years(age).Should().Be(expected);
}
