using DzhusShelter.Api.Application.Abstractions;
using DzhusShelter.Api.Application.Birthdays.Dtos;
using DzhusShelter.Api.Domain.Common;

namespace DzhusShelter.Api.Application.Birthdays.Queries;

public sealed class GetBirthdaysQueryHandler : IQueryHandler<GetBirthdaysQuery, Result<IReadOnlyList<BirthdayDto>>>
{
    private readonly IBirthdayRepository _repository;
    private readonly IBirthdayClock _clock;

    public GetBirthdaysQueryHandler(IBirthdayRepository repository, IBirthdayClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<Result<IReadOnlyList<BirthdayDto>>> Handle(GetBirthdaysQuery query, CancellationToken cancellationToken)
    {
        var today = _clock.Today;
        var birthdays = await _repository.GetAllAsync(cancellationToken);

        IReadOnlyList<BirthdayDto> dtos = birthdays
            .Select(b =>
            {
                var next = b.NextOccurrence(today);
                return new BirthdayDto(b.Id, b.Name, b.Day, b.Month, b.Year, next, next.DayNumber - today.DayNumber, b.AgeOn(next));
            })
            .OrderBy(d => d.NextOccurrence)
            .ThenBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Result.Success(dtos);
    }
}
