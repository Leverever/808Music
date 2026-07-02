namespace _808Music.Application.Common.Crud.Contracts;

public interface IUpdateHandler<TKey, TRequest, TResponse>
    where TResponse : class
{
    Task<TResponse?> Handle(
        TKey id,
        TRequest request,
        CancellationToken cancellationToken = default);
}
