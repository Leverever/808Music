using _808Music.Application.Common.Crud.Contracts;
using _808Music.Application.Common.Search;
using Microsoft.AspNetCore.Mvc;

namespace RS1_2024_25.API.Controllers.V2.Common;

public abstract class FullCrudControllerBase<TKey, TCreateRequest, TUpdateRequest, TSearch, TResponse>
    : CrudControllerBase
    where TSearch : BaseSearchObject, new()
    where TResponse : class
{
    private readonly ICrudHandler<TKey, TCreateRequest, TUpdateRequest, TSearch, TResponse> _handler;

    protected FullCrudControllerBase(
        ICrudHandler<TKey, TCreateRequest, TUpdateRequest, TSearch, TResponse> handler)
    {
        _handler = handler;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public virtual Task<ActionResult<PagedResult<TResponse>>> List(
        [FromQuery] TSearch? search,
        CancellationToken cancellationToken)
    {
        return ListAsync(_handler, search, cancellationToken);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public virtual Task<ActionResult<TResponse>> GetById(
        [FromRoute] TKey id,
        CancellationToken cancellationToken)
    {
        return GetByIdAsync(_handler, id, cancellationToken);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public virtual Task<ActionResult<TResponse>> Create(
        [FromBody] TCreateRequest request,
        CancellationToken cancellationToken)
    {
        return CreateAsync(_handler, request, cancellationToken);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public virtual Task<ActionResult<TResponse>> Update(
        [FromRoute] TKey id,
        [FromBody] TUpdateRequest request,
        CancellationToken cancellationToken)
    {
        return UpdateAsync(_handler, id, request, cancellationToken);
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public virtual Task<IActionResult> Delete(
        [FromRoute] TKey id,
        CancellationToken cancellationToken)
    {
        return DeleteAsync(_handler, id, cancellationToken);
    }
}
