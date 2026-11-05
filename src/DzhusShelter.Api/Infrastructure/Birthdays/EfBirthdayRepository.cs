using DzhusShelter.Api.Application.Birthdays;
using DzhusShelter.Api.Domain.Birthdays;
using Microsoft.EntityFrameworkCore;

namespace DzhusShelter.Api.Infrastructure.Birthdays;

public sealed class EfBirthdayRepository : IBirthdayRepository
{
    private readonly AppDbContext _dbContext;

    public EfBirthdayRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(Birthday birthday, CancellationToken cancellationToken)
    {
        await _dbContext.Birthdays.AddAsync(birthday, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<Birthday?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _dbContext.Birthdays.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Birthday>> GetAllAsync(CancellationToken cancellationToken) =>
        await _dbContext.Birthdays.AsNoTracking().ToListAsync(cancellationToken);

    public async Task DeleteAsync(Birthday birthday, CancellationToken cancellationToken)
    {
        _dbContext.Birthdays.Remove(birthday);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        _dbContext.SaveChangesAsync(cancellationToken);
}
