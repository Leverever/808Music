namespace _808Music.Application.Common.Crud.Contracts;

public interface IDeleteHandler<TKey>
{
    Task<bool> Handle(
        TKey id,
        CancellationToken cancellationToken = default);
}
