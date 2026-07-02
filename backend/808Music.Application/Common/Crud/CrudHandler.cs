using _808Music.Application.Common.Crud.Contracts;
using _808Music.Application.Common.Persistence;
using _808Music.Application.Common.Search;
using AutoMapper;

namespace _808Music.Application.Common.Crud;

public abstract class CrudHandler<TEntity, TKey, TCreateRequest, TUpdateRequest, TSearch, TResponse>
    : ReadOnlyCrudHandler<TEntity, TKey, TSearch, TResponse>,
        ICrudHandler<TKey, TCreateRequest, TUpdateRequest, TSearch, TResponse>
    where TEntity : class
    where TSearch : BaseSearchObject
    where TResponse : class
{
    protected readonly IUnitOfWork UnitOfWork;

    protected CrudHandler(
        ISearchRepository<TEntity, TKey, TSearch> repository,
        IUnitOfWork unitOfWork,
        IMapper mapper)
        : base(repository, mapper)
    {
        UnitOfWork = unitOfWork;
    }

    public virtual async Task<TResponse> Create(
        TCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = Mapper.Map<TEntity>(request);

        await BeforeCreateAsync(request, entity, cancellationToken);
        await Repository.AddAsync(entity, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);
        await AfterCreateAsync(request, entity, cancellationToken);

        return Mapper.Map<TResponse>(entity);
    }

    public virtual async Task<TResponse?> Update(
        TKey id,
        TUpdateRequest request,
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

    public virtual async Task<bool> Delete(
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

    protected virtual Task BeforeCreateAsync(
        TCreateRequest request,
        TEntity entity,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    protected virtual Task AfterCreateAsync(
        TCreateRequest request,
        TEntity entity,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    protected virtual Task BeforeUpdateAsync(
        TKey id,
        TUpdateRequest request,
        TEntity entity,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    protected virtual Task AfterUpdateAsync(
        TKey id,
        TUpdateRequest request,
        TEntity entity,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
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
