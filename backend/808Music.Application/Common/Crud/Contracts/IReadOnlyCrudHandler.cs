using _808Music.Application.Common.Search;

namespace _808Music.Application.Common.Crud.Contracts;

public interface IReadOnlyCrudHandler<TKey, TSearch, TResponse>
    where TSearch : BaseSearchObject
    where TResponse : class
{
    Task<TResponse?> GetById(
        TKey id,
        CancellationToken cancellationToken = default);

    Task<PagedResult<TResponse>> List(
        TSearch search,
        CancellationToken cancellationToken = default);
}
