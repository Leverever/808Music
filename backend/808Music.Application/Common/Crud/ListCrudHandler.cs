using _808Music.Application.Common.Crud.Contracts;
using _808Music.Application.Common.Persistence;
using _808Music.Application.Common.Search;
using AutoMapper;

namespace _808Music.Application.Common.Crud;

public abstract class ListCrudHandler<TEntity, TKey, TSearch, TResponse>
    : IListHandler<TSearch, TResponse>
    where TEntity : class
    where TSearch : BaseSearchObject
    where TResponse : class
{
    protected readonly IMapper Mapper;
    protected readonly ISearchRepository<TEntity, TKey, TSearch> Repository;

    protected ListCrudHandler(
        ISearchRepository<TEntity, TKey, TSearch> repository,
        IMapper mapper)
    {
        Repository = repository;
        Mapper = mapper;
    }

    public virtual async Task<PagedResult<TResponse>> Handle(
        TSearch search,
        CancellationToken cancellationToken = default)
    {
        var result = await Repository.ListAsync(search, cancellationToken);
        var items = Mapper.Map<IReadOnlyList<TResponse>>(result.Items);

        return new PagedResult<TResponse>(
            items,
            result.Page,
            result.PageSize,
            result.TotalCount);
    }
}
