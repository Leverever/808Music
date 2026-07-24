using _808Music.Application.Common.Search;

namespace _808Music.Application.Common.Crud.Contracts;

public interface IListHandler<TSearch, TResponse>
    where TSearch : BaseSearchObject
    where TResponse : class
{
    Task<PagedResult<TResponse>> Handle(
        TSearch search,
        CancellationToken cancellationToken = default);
}
