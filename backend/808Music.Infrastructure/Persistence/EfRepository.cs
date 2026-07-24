using _808Music.Application.Common.Persistence;
using _808Music.Application.Common.Search;
using Microsoft.EntityFrameworkCore;

namespace _808Music.Infrastructure.Persistence;

public class EfRepository<TEntity, TKey> : IRepository<TEntity, TKey>
    where TEntity : class
{
    protected readonly DbContext DbContext;
    protected readonly DbSet<TEntity> Set;

    public EfRepository(DbContext dbContext)
    {
        DbContext = dbContext;
        Set = dbContext.Set<TEntity>();
    }

    public virtual Task<TEntity?> GetByIdAsync(
        TKey id,
        CancellationToken cancellationToken = default)
    {
        return Set.FindAsync([id], cancellationToken).AsTask();
    }

    public virtual async Task<IReadOnlyList<TEntity>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        return await Set.ToListAsync(cancellationToken);
    }

    public virtual async Task AddAsync(
        TEntity entity,
        CancellationToken cancellationToken = default)
    {
        await Set.AddAsync(entity, cancellationToken);
    }

    public virtual async Task AddRangeAsync(
        IEnumerable<TEntity> entities,
        CancellationToken cancellationToken = default)
    {
        await Set.AddRangeAsync(entities, cancellationToken);
    }

    public virtual void Update(TEntity entity)
    {
        Set.Update(entity);
    }

    public virtual void Remove(TEntity entity)
    {
        Set.Remove(entity);
    }
}

public class EfRepository<TEntity, TKey, TSearch> :
    EfRepository<TEntity, TKey>,
    ISearchRepository<TEntity, TKey, TSearch>
    where TEntity : class
    where TSearch : BaseSearchObject
{
    public EfRepository(DbContext dbContext)
        : base(dbContext)
    {
    }

    public virtual async Task<PagedResult<TEntity>> ListAsync(
        TSearch search,
        CancellationToken cancellationToken = default)
    {
        var query = Set.AsQueryable();

        query = ApplyFiltering(query, search);
        query = ApplySorting(query, search);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip(search.Skip)
            .Take(search.Take)
            .ToListAsync(cancellationToken);

        return new PagedResult<TEntity>(
            items,
            search.NormalizedPage,
            search.NormalizedPageSize,
            totalCount);
    }

    protected virtual IQueryable<TEntity> ApplyFiltering(
        IQueryable<TEntity> query,
        TSearch search)
    {
        return query;
    }

    protected virtual IQueryable<TEntity> ApplySorting(
        IQueryable<TEntity> query,
        TSearch search)
    {
        return query;
    }
}
