using DzhusShelter.Api.Application.Birthdays;
using DzhusShelter.Api.Application.Birthdays.Commands;
using DzhusShelter.Api.Domain.Birthdays;
using FluentAssertions;
using NSubstitute;

namespace DzhusShelter.Api.Tests.Application.Birthdays;

public class BirthdayCrudHandlersTests
{
    private static readonly DateOnly Today = new(2026, 11, 5);
    private readonly StubBirthdayClock _clock = new(Today);
    private readonly IBirthdayRepository _repository = Substitute.For<IBirthdayRepository>();

    private Birthday Existing(int day = 6, int month = 11, int? year = null, string name = "Оля") =>
        Birthday.Create(name, day, month, year, Today, _clock.UtcNow).Value;

    [Fact]
    public async Task Create_Valid_AddsAndReturnsId()
    {
        var handler = new CreateBirthdayCommandHandler(_repository, new CreateBirthdayCommandValidator(), _clock);

        var result = await handler.Handle(new CreateBirthdayCommand("Саша Пупкін", 12, 11, 2001), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _repository.Received(1).AddAsync(
            Arg.Is<Birthday>(b => b.Id == result.Value && b.Name == "Саша Пупкін" && b.Year == 2001 && b.CreatedAt == _clock.UtcNow),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_DomainFailure_ReturnsErrorAndDoesNotAdd()
    {
        var handler = new CreateBirthdayCommandHandler(_repository, new CreateBirthdayCommandValidator(), _clock);

        var result = await handler.Handle(new CreateBirthdayCommand("Саша", 31, 4, null), CancellationToken.None);

        result.Error.Should().Be(BirthdayErrors.InvalidDate);
        await _repository.DidNotReceive().AddAsync(Arg.Any<Birthday>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_ValidatorFailure_ReturnsValidationError()
    {
        var handler = new CreateBirthdayCommandHandler(_repository, new CreateBirthdayCommandValidator(), _clock);

        var result = await handler.Handle(new CreateBirthdayCommand("Саша", 1, 13, null), CancellationToken.None);

        result.Error.Code.Should().Be("Birthdays.Validation");
        await _repository.DidNotReceive().AddAsync(Arg.Any<Birthday>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_Existing_UpdatesAndSaves()
    {
        var birthday = Existing();
        _repository.GetByIdAsync(birthday.Id, Arg.Any<CancellationToken>()).Returns(birthday);
        var handler = new UpdateBirthdayCommandHandler(_repository, new UpdateBirthdayCommandValidator(), _clock);

        var result = await handler.Handle(new UpdateBirthdayCommand(birthday.Id, "Оля Коваль", 6, 11, 1999), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        birthday.Year.Should().Be(1999);
        birthday.Name.Should().Be("Оля Коваль");
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_DomainFailure_DoesNotSave()
    {
        var birthday = Existing();
        _repository.GetByIdAsync(birthday.Id, Arg.Any<CancellationToken>()).Returns(birthday);
        var handler = new UpdateBirthdayCommandHandler(_repository, new UpdateBirthdayCommandValidator(), _clock);

        var result = await handler.Handle(new UpdateBirthdayCommand(birthday.Id, "Оля", 31, 4, null), CancellationToken.None);

        result.Error.Should().Be(BirthdayErrors.InvalidDate);
        await _repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_Missing_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((Birthday?)null);
        var handler = new UpdateBirthdayCommandHandler(_repository, new UpdateBirthdayCommandValidator(), _clock);

        var result = await handler.Handle(new UpdateBirthdayCommand(id, "Оля", 6, 11, null), CancellationToken.None);

        result.Error.Code.Should().Be("Birthday.NotFound");
        await _repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_Existing_Deletes()
    {
        var birthday = Existing();
        _repository.GetByIdAsync(birthday.Id, Arg.Any<CancellationToken>()).Returns(birthday);
        var handler = new DeleteBirthdayCommandHandler(_repository);

        var result = await handler.Handle(new DeleteBirthdayCommand(birthday.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _repository.Received(1).DeleteAsync(birthday, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_Missing_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((Birthday?)null);
        var handler = new DeleteBirthdayCommandHandler(_repository);

        var result = await handler.Handle(new DeleteBirthdayCommand(id), CancellationToken.None);

        result.Error.Code.Should().Be("Birthday.NotFound");
        await _repository.DidNotReceive().DeleteAsync(Arg.Any<Birthday>(), Arg.Any<CancellationToken>());
    }
}
