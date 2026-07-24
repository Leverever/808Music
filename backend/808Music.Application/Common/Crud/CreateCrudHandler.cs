using _808Music.Application.Common.Crud.Contracts;
using _808Music.Application.Common.Persistence;
using AutoMapper;

namespace _808Music.Application.Common.Crud;

public abstract class CreateCrudHandler<TEntity, TKey, TRequest, TResponse>
    : ICreateHandler<TRequest, TResponse>
    where TEntity : class
    where TResponse : class
{
    protected readonly IMapper Mapper;
    protected readonly IRepository<TEntity, TKey> Repository;
    protected readonly IUnitOfWork UnitOfWork;

    protected CreateCrudHandler(
        IRepository<TEntity, TKey> repository,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        Repository = repository;
        UnitOfWork = unitOfWork;
        Mapper = mapper;
    }

    public virtual async Task<TResponse> Handle(
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = Mapper.Map<TEntity>(request);

        await BeforeCreateAsync(request, entity, cancellationToken);
        await Repository.AddAsync(entity, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);
        await AfterCreateAsync(request, entity, cancellationToken);

        return Mapper.Map<TResponse>(entity);
    }

    protected virtual Task BeforeCreateAsync(
        TRequest request,
        TEntity entity,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    protected virtual Task AfterCreateAsync(
        TRequest request,
        TEntity entity,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
