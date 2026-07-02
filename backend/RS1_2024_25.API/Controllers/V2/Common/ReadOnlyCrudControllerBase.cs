using _808Music.Application.Common.Crud.Contracts;
using _808Music.Application.Common.Search;
using Microsoft.AspNetCore.Mvc;

namespace RS1_2024_25.API.Controllers.V2.Common;

public abstract class ReadOnlyCrudControllerBase<TKey, TSearch, TResponse>
    : CrudControllerBase
    where TSearch : BaseSearchObject, new()
    where TResponse : class
{
    private readonly IReadOnlyCrudHandler<TKey, TSearch, TResponse> _handler;

    protected ReadOnlyCrudControllerBase(
        IReadOnlyCrudHandler<TKey, TSearch, TResponse> handler)
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
}
