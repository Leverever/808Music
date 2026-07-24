using _808Music.Application.Common.Search;

namespace _808Music.Application.Common.Crud.Contracts;

public interface ICrudHandler<TKey, TCreateRequest, TUpdateRequest, TSearch, TResponse>
    : IReadOnlyCrudHandler<TKey, TSearch, TResponse>
    where TSearch : BaseSearchObject
    where TResponse : class
{
    Task<TResponse> Create(
        TCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<TResponse?> Update(
        TKey id,
        TUpdateRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> Delete(
        TKey id,
        CancellationToken cancellationToken = default);
}
