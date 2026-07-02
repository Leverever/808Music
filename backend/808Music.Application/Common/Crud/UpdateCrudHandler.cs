using _808Music.Application.Common.Crud.Contracts;
using _808Music.Application.Common.Persistence;
using AutoMapper;

namespace _808Music.Application.Common.Crud;

public abstract class UpdateCrudHandler<TEntity, TKey, TRequest, TResponse>
    : IUpdateHandler<TKey, TRequest, TResponse>
    where TEntity : class
    where TResponse : class
{
    protected readonly IMapper Mapper;
    protected readonly IRepository<TEntity, TKey> Repository;
    protected readonly IUnitOfWork UnitOfWork;

    protected UpdateCrudHandler(
        IRepository<TEntity, TKey> repository,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        Repository = repository;
        UnitOfWork = unitOfWork;
        Mapper = mapper;
    }

    public virtual async Task<TResponse?> Handle(
        TKey id,
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await Repository.GetByIdAsync(id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        Mapper.Map(request, entity);

        await BeforeUpdateAsync(id, request, entity, cancellationToken);
        Repository.Update(entity);
        await UnitOfWork.SaveChangesAsync(cancellationToken);
        await AfterUpdateAsync(id, request, entity, cancellationToken);

        return Mapper.Map<TResponse>(entity);
    }

    protected virtual Task BeforeUpdateAsync(
        TKey id,
        TRequest request,
        TEntity entity,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    protected virtual Task AfterUpdateAsync(
        TKey id,
        TRequest request,
        TEntity entity,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
