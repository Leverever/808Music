using _808Music.Application.Common.Crud.Contracts;
using _808Music.Application.Common.Search;
using _808Music.Application.Genres;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using RS1_2024_25.API.Controllers.V2.Common;

namespace RS1_2024_25.API.Controllers.V2;

[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/genres")]
public sealed class GenresController : FullCrudControllerBase
    <int, GenreInsUpdCommand, GenreInsUpdCommand, GenreSearchObject, GenreResponse>
{
    public GenresController(ICrudHandler<int, GenreInsUpdCommand, GenreInsUpdCommand, GenreSearchObject, GenreResponse> handler) : base(handler)
    {
    }

}
