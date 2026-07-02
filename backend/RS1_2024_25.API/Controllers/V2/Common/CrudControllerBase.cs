using _808Music.Application.Common.Crud.Contracts;
using _808Music.Application.Common.Search;
using Microsoft.AspNetCore.Mvc;

namespace RS1_2024_25.API.Controllers.V2.Common;

[ApiController]
[Produces("application/json")]
public abstract class CrudControllerBase : ControllerBase
{
    protected async Task<ActionResult<PagedResult<TResponse>>> ListAsync<TKey, TSearch, TResponse>(
        IReadOnlyCrudHandler<TKey, TSearch, TResponse> handler,
        TSearch? search,
        CancellationToken cancellationToken)
        where TSearch : BaseSearchObject, new()
        where TResponse : class
    {
        var result = await handler.List(search ?? new TSearch(), cancellationToken);

        return Ok(result);
    }

    protected async Task<ActionResult<TResponse>> GetByIdAsync<TKey, TSearch, TResponse>(
        IReadOnlyCrudHandler<TKey, TSearch, TResponse> handler,
        TKey id,
        CancellationToken cancellationToken)
        where TSearch : BaseSearchObject
        where TResponse : class
    {
        var response = await handler.GetById(id, cancellationToken);

        return response is null ? NotFound() : Ok(response);
    }

    protected async Task<ActionResult<PagedResult<TResponse>>> ListAsync<TKey, TCreateRequest, TUpdateRequest, TSearch, TResponse>(
        ICrudHandler<TKey, TCreateRequest, TUpdateRequest, TSearch, TResponse> handler,
        TSearch? search,
        CancellationToken cancellationToken)
        where TSearch : BaseSearchObject, new()
        where TResponse : class
    {
        var result = await handler.List(search ?? new TSearch(), cancellationToken);

        return Ok(result);
    }

    protected async Task<ActionResult<TResponse>> GetByIdAsync<TKey, TCreateRequest, TUpdateRequest, TSearch, TResponse>(
        ICrudHandler<TKey, TCreateRequest, TUpdateRequest, TSearch, TResponse> handler,
        TKey id,
        CancellationToken cancellationToken)
        where TSearch : BaseSearchObject
        where TResponse : class
    {
        var response = await handler.GetById(id, cancellationToken);

        return response is null ? NotFound() : Ok(response);
    }

    protected async Task<ActionResult<TResponse>> CreateAsync<TKey, TCreateRequest, TUpdateRequest, TSearch, TResponse>(
        ICrudHandler<TKey, TCreateRequest, TUpdateRequest, TSearch, TResponse> handler,
        TCreateRequest request,
        CancellationToken cancellationToken)
        where TSearch : BaseSearchObject
        where TResponse : class
    {
        var response = await handler.Create(request, cancellationToken);

        return Ok(response);
    }

    protected async Task<ActionResult<TResponse>> UpdateAsync<TKey, TCreateRequest, TUpdateRequest, TSearch, TResponse>(
        ICrudHandler<TKey, TCreateRequest, TUpdateRequest, TSearch, TResponse> handler,
        TKey id,
        TUpdateRequest request,
        CancellationToken cancellationToken)
        where TSearch : BaseSearchObject
        where TResponse : class
    {
        var response = await handler.Update(id, request, cancellationToken);

        return response is null ? NotFound() : Ok(response);
    }

    protected async Task<IActionResult> DeleteAsync<TKey, TCreateRequest, TUpdateRequest, TSearch, TResponse>(
        ICrudHandler<TKey, TCreateRequest, TUpdateRequest, TSearch, TResponse> handler,
        TKey id,
        CancellationToken cancellationToken)
        where TSearch : BaseSearchObject
        where TResponse : class
    {
        var deleted = await handler.Delete(id, cancellationToken);

        return deleted ? NoContent() : NotFound();
    }

    protected async Task<ActionResult<PagedResult<TResponse>>> ListAsync<TSearch, TResponse>(
        IListHandler<TSearch, TResponse> handler,
        TSearch? search,
        CancellationToken cancellationToken)
        where TSearch : BaseSearchObject, new()
        where TResponse : class
    {
        var result = await handler.Handle(search ?? new TSearch(), cancellationToken);

        return Ok(result);
    }

    protected async Task<ActionResult<TResponse>> GetByIdAsync<TKey, TResponse>(
        IGetByIdHandler<TKey, TResponse> handler,
        TKey id,
        CancellationToken cancellationToken)
        where TResponse : class
    {
        var response = await handler.Handle(id, cancellationToken);

        return response is null ? NotFound() : Ok(response);
    }

    protected async Task<ActionResult<TResponse>> CreateAsync<TRequest, TResponse>(
        ICreateHandler<TRequest, TResponse> handler,
        TRequest request,
        CancellationToken cancellationToken)
        where TResponse : class
    {
        var response = await handler.Handle(request, cancellationToken);

        return Ok(response);
    }

    protected async Task<ActionResult<TResponse>> UpdateAsync<TKey, TRequest, TResponse>(
        IUpdateHandler<TKey, TRequest, TResponse> handler,
        TKey id,
        TRequest request,
        CancellationToken cancellationToken)
        where TResponse : class
    {
        var response = await handler.Handle(id, request, cancellationToken);

        return response is null ? NotFound() : Ok(response);
    }

    protected async Task<IActionResult> DeleteAsync<TKey>(
        IDeleteHandler<TKey> handler,
        TKey id,
        CancellationToken cancellationToken)
    {
        var deleted = await handler.Handle(id, cancellationToken);

        return deleted ? NoContent() : NotFound();
    }
}
