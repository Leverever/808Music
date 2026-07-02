namespace _808Music.Application.Common.Crud.Contracts;

public interface ICreateHandler<TRequest, TResponse>
{
    Task<TResponse> Handle(
        TRequest request,
        CancellationToken cancellationToken = default);
}
