using DzhusShelter.Api.Application.Birthdays;
using DzhusShelter.Api.Domain.Birthdays;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DzhusShelter.Api.Infrastructure.Birthdays;

public sealed class EfBirthdayNotificationRepository : IBirthdayNotificationRepository
{
    private const string UniqueViolationSqlState = "23505";

    private readonly AppDbContext _dbContext;

    public EfBirthdayNotificationRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> ExistsAsync(BirthdayNotificationKind kind, DateOnly forDate, CancellationToken cancellationToken) =>
        _dbContext.BirthdayNotifications.AnyAsync(n => n.Kind == kind && n.ForDate == forDate, cancellationToken);

    public async Task<bool> TryAddAsync(BirthdayNotification notification, CancellationToken cancellationToken)
    {
        await _dbContext.BirthdayNotifications.AddAsync(notification, cancellationToken);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: UniqueViolationSqlState })
        {
            // Another writer got there first; detach so the scoped context stays usable.
            _dbContext.Entry(notification).State = EntityState.Detached;
            return false;
        }
    }
}
