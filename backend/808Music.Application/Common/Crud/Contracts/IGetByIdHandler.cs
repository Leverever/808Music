namespace _808Music.Application.Common.Crud.Contracts;

public interface IGetByIdHandler<TKey, TResponse>
    where TResponse : class
{
    Task<TResponse?> Handle(
        TKey id,
        CancellationToken cancellationToken = default);
}
