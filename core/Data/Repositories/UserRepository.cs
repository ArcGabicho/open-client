using Microsoft.EntityFrameworkCore;
using OpenClient.Models.Domain;
using OpenClient.Models.DTO.Users;

namespace OpenClient.Data.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly IDbContextFactory<OpenClientDbContext> _contextFactory;
    private readonly ILogger<UserRepository> _logger;

    public UserRepository(
        IDbContextFactory<OpenClientDbContext> contextFactory,
        ILogger<UserRepository> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task<(IReadOnlyList<User> Items, int TotalCount)> GetPagedAsync(
        UserSearchFilter filter,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);

        IQueryable<User> query = db.Users.AsNoTracking();

        query = ApplySearch(query, filter.Search);
        query = ApplyStatus(query, filter.Status);
        query = ApplyRole(query, filter.Role);

        var total = await query.CountAsync(cancellationToken);

        var items = await ApplySort(query, filter.SortBy, filter.SortDir)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);

        return await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<bool> EmailExistsAsync(
        string email,
        int? excludeUserId = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);

        return await db.Users
            .AsNoTracking()
            .AnyAsync(u => u.Email == email && (excludeUserId == null || u.Id != excludeUserId), cancellationToken);
    }

    public async Task<bool> UserNameExistsAsync(
        string userName,
        int? excludeUserId = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);

        return await db.Users
            .AsNoTracking()
            .AnyAsync(u => u.UserName == userName && (excludeUserId == null || u.Id != excludeUserId), cancellationToken);
    }

    public async Task<int> CountActiveAdminsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);

        return await db.Users
            .AsNoTracking()
            .CountAsync(u => u.IsActive && u.Role == UserRoles.Admin, cancellationToken);
    }

    public async Task<int> AddAsync(User user, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Usuario creado: Id={UserId}.", user.Id);
        return user.Id;
    }

    public async Task<UserUpdateOutcome> MutateAsync(
        int id,
        string? expectedConcurrencyStamp,
        Action<User> apply,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        if (user is null)
        {
            return UserUpdateOutcome.NotFound;
        }

        if (expectedConcurrencyStamp is not null
            && !string.Equals(user.ConcurrencyStamp, expectedConcurrencyStamp, StringComparison.Ordinal))
        {
            return UserUpdateOutcome.ConcurrencyConflict;
        }

        apply(user);
        user.ConcurrencyStamp = Guid.NewGuid().ToString("N");

        await db.SaveChangesAsync(cancellationToken);
        return UserUpdateOutcome.Updated;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        if (user is null)
        {
            return false;
        }

        db.Users.Remove(user);
        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Usuario eliminado: Id={UserId}.", id);
        return true;
    }

    // ---------- Composición de la consulta ----------

    private static IQueryable<User> ApplySearch(IQueryable<User> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return query;
        }

        var term = search.Trim();

        return query.Where(u =>
            u.FirstName.Contains(term) ||
            u.LastName.Contains(term) ||
            u.UserName.Contains(term) ||
            u.Email.Contains(term));
    }

    private static IQueryable<User> ApplyStatus(IQueryable<User> query, UserStatusFilter status) => status switch
    {
        UserStatusFilter.Active => query.Where(u => u.IsActive),
        UserStatusFilter.Inactive => query.Where(u => !u.IsActive),
        _ => query
    };

    private static IQueryable<User> ApplyRole(IQueryable<User> query, string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return query;
        }

        var value = role.Trim();
        return query.Where(u => u.Role == value);
    }

    private static IQueryable<User> ApplySort(IQueryable<User> query, string? sortBy, string? sortDir)
    {
        var descending = !string.Equals(sortDir, "asc", StringComparison.OrdinalIgnoreCase);

        return (sortBy?.ToLowerInvariant()) switch
        {
            "name" => descending
                ? query.OrderByDescending(u => u.FirstName).ThenByDescending(u => u.LastName).ThenBy(u => u.Id)
                : query.OrderBy(u => u.FirstName).ThenBy(u => u.LastName).ThenBy(u => u.Id),
            "username" => descending
                ? query.OrderByDescending(u => u.UserName).ThenBy(u => u.Id)
                : query.OrderBy(u => u.UserName).ThenBy(u => u.Id),
            "email" => descending
                ? query.OrderByDescending(u => u.Email).ThenBy(u => u.Id)
                : query.OrderBy(u => u.Email).ThenBy(u => u.Id),
            "status" => descending
                ? query.OrderByDescending(u => u.IsActive).ThenBy(u => u.Id)
                : query.OrderBy(u => u.IsActive).ThenBy(u => u.Id),
            _ => descending
                ? query.OrderByDescending(u => u.CreatedAt).ThenByDescending(u => u.Id)
                : query.OrderBy(u => u.CreatedAt).ThenBy(u => u.Id)
        };
    }
}