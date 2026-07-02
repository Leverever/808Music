using _808Music.Application.Common.Crud.Contracts;
using _808Music.Application.Common.Persistence;

namespace _808Music.Application.Common.Crud;

public abstract class DeleteCrudHandler<TEntity, TKey>
    : IDeleteHandler<TKey>
    where TEntity : class
{
    protected readonly IRepository<TEntity, TKey> Repository;
    protected readonly IUnitOfWork UnitOfWork;

    protected DeleteCrudHandler(
        IRepository<TEntity, TKey> repository,
        IUnitOfWork unitOfWork)
    {
        Repository = repository;
        UnitOfWork = unitOfWork;
    }

    public virtual async Task<bool> Handle(
        TKey id,
        CancellationToken cancellationToken = default)
    {
        var entity = await Repository.GetByIdAsync(id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        await BeforeDeleteAsync(id, entity, cancellationToken);
        Repository.Remove(entity);
        await UnitOfWork.SaveChangesAsync(cancellationToken);
        await AfterDeleteAsync(id, entity, cancellationToken);

        return true;
    }

    protected virtual Task BeforeDeleteAsync(
        TKey id,
        TEntity entity,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    protected virtual Task AfterDeleteAsync(
        TKey id,
        TEntity entity,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
