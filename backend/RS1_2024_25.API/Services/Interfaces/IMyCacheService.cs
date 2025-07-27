namespace RS1_2024_25.API.Services.Interfaces
{
    public interface IMyCacheService
    {
        Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;
        Task<T> GetAsync<T>(string key,Func<Task<T>> factory, CancellationToken cancellationToken = default) where T : class;
        Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default) where T : class;
        Task RemoveAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;
        Task RemoveWithPrefixAsync<T>(string keyPrefix, CancellationToken cancellationToken = default) where T : class;
        Task<Stream?> GetStreamAsync(string key, CancellationToken cancellationToken = default);
        Task<Stream> GetStreamAsync(string key, Func<Task<byte[]>> factory, CancellationToken cancellationToken = default);
        Task SetAsync(string key, byte[] value, CancellationToken cancellationToken = default);

    }
}
