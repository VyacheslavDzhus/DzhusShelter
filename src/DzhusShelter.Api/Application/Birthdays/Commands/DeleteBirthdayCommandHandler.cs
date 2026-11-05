using DzhusShelter.Api.Application.Abstractions;
using DzhusShelter.Api.Domain.Birthdays;
using DzhusShelter.Api.Domain.Common;

namespace DzhusShelter.Api.Application.Birthdays.Commands;

public sealed class DeleteBirthdayCommandHandler : ICommandHandler<DeleteBirthdayCommand, Result>
{
    private readonly IBirthdayRepository _repository;

    public DeleteBirthdayCommandHandler(IBirthdayRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result> Handle(DeleteBirthdayCommand command, CancellationToken cancellationToken)
    {
        var birthday = await _repository.GetByIdAsync(command.Id, cancellationToken);
        if (birthday is null)
            return Result.Failure(BirthdayErrors.NotFound(command.Id));

        await _repository.DeleteAsync(birthday, cancellationToken);
        return Result.Success();
    }
}
