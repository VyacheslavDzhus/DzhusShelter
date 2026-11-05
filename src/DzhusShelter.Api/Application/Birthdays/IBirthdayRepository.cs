using DzhusShelter.Api.Domain.Birthdays;

namespace DzhusShelter.Api.Application.Birthdays;

public interface IBirthdayRepository
{
    /// <summary>Persists immediately.</summary>
    Task AddAsync(Birthday birthday, CancellationToken cancellationToken);

    /// <summary>Returns a tracked entity; call <see cref="SaveChangesAsync"/> to persist changes made via Update.</summary>
    Task<Birthday?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Birthday>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>Persists immediately.</summary>
    Task DeleteAsync(Birthday birthday, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
