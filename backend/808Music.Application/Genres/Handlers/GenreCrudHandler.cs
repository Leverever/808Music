using _808Music.Application.Common.Crud;
using _808Music.Application.Common.Persistence;
using _808Music.Application.Common.Search;
using _808Music.Domain.Static;
using AutoMapper;

namespace _808Music.Application.Genres.Handlers;

public sealed class GenreCrudHandler
    : CrudHandler<Genre, int, GenreInsUpdCommand, GenreInsUpdCommand, GenreSearchObject, GenreResponse>
{
    public GenreCrudHandler(
        ISearchRepository<Genre, int, GenreSearchObject> repository,
        IUnitOfWork unitOfWork,
        IMapper mapper)
        : base(repository, unitOfWork, mapper)
    {
    }
}
