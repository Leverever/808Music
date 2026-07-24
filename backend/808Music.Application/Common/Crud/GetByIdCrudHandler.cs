using _808Music.Application.Common.Crud.Contracts;
using _808Music.Application.Common.Persistence;
using AutoMapper;

namespace _808Music.Application.Common.Crud;

public abstract class GetByIdCrudHandler<TEntity, TKey, TResponse>
    : IGetByIdHandler<TKey, TResponse>
    where TEntity : class
    where TResponse : class
{
    protected readonly IMapper Mapper;
    protected readonly IRepository<TEntity, TKey> Repository;

    protected GetByIdCrudHandler(
        IRepository<TEntity, TKey> repository,
        IMapper mapper)
    {
        Repository = repository;
        Mapper = mapper;
    }

    public virtual async Task<TResponse?> Handle(
        TKey id,
        CancellationToken cancellationToken = default)
    {
        var entity = await Repository.GetByIdAsync(id, cancellationToken);

        return entity is null ? null : Mapper.Map<TResponse>(entity);
    }
}
