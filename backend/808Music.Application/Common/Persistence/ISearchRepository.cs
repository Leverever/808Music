using _808Music.Application.Common.Search;

namespace _808Music.Application.Common.Persistence;

public interface ISearchRepository<TEntity, TKey, TSearch> : IRepository<TEntity, TKey>
    where TEntity : class
    where TSearch : BaseSearchObject
{
    Task<PagedResult<TEntity>> ListAsync(
        TSearch search,
        CancellationToken cancellationToken = default);
}
