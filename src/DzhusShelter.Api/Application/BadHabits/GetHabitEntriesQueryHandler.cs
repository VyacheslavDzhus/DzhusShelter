using DzhusShelter.Api.Application.Abstractions;
using DzhusShelter.Api.Domain.Common;
using FluentValidation;

namespace DzhusShelter.Api.Application.BadHabits;

public sealed class GetHabitEntriesQueryHandler : IQueryHandler<GetHabitEntriesQuery, Result<IReadOnlyList<HabitEntryDto>>>
{
    private readonly IHabitEntryRepository _repository;
    private readonly IValidator<GetHabitEntriesQuery> _validator;

    public GetHabitEntriesQueryHandler(IHabitEntryRepository repository, IValidator<GetHabitEntriesQuery> validator)
    {
        _repository = repository;
        _validator = validator;
    }

    public async Task<Result<IReadOnlyList<HabitEntryDto>>> Handle(GetHabitEntriesQuery query, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(query, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result.Failure<IReadOnlyList<HabitEntryDto>>(
                new Error("BadHabits.Validation", validationResult.Errors[0].ErrorMessage));
        }

        var entries = await _repository.GetAsync(query.From, query.To, query.HabitType, query.SubType, cancellationToken);
        IReadOnlyList<HabitEntryDto> dtos = entries
            .Select(e => new HabitEntryDto(e.Id, e.HabitType, e.SubType, e.OccurredAt, e.Notes))
            .ToList();

        return Result.Success(dtos);
    }
}
