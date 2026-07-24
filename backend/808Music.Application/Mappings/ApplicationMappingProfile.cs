using _808Music.Application.Genres;
using _808Music.Domain.Static;
using AutoMapper;

namespace _808Music.Application.Mappings;

public sealed class ApplicationMappingProfile : Profile
{
    public ApplicationMappingProfile()
    {
        // Add simple CRUD DTO/entity mappings here as V2 modules are migrated.
        CreateMap<Genre, GenreResponse>();
        CreateMap<GenreInsUpdCommand, Genre>();
    }
}
