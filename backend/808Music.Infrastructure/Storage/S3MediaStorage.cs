using _808Music.Application.Abstractions;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using System.Net;

namespace _808Music.Infrastructure.Storage;

public sealed class S3MediaStorage : IMediaStorage
{
    private readonly IAmazonS3 _s3;
    private readonly S3Options _options;
    private readonly IAmazonS3 _publicS3;

    public S3MediaStorage(IAmazonS3 s3, IOptions<S3Options> options)
    {
        _s3 = s3;
        _options = options.Value;
        _publicS3 = CreatePublicS3Client(_options);
    }

    public async Task<StoredMediaObject> UploadAsync(
        UploadMediaObject request,
        CancellationToken cancellationToken = default)
    {
        var putRequest = new PutObjectRequest
        {
            BucketName = _options.Bucket,
            Key = request.ObjectKey,
            InputStream = request.Content,
            ContentType = request.ContentType
        };

        foreach (var item in request.Metadata ?? new Dictionary<string, string>())
        {
            putRequest.Metadata[item.Key] = item.Value;
        }

        await _s3.PutObjectAsync(putRequest, cancellationToken);

        return new StoredMediaObject(
            request.ObjectKey,
            request.ContentType,
            request.Content.CanSeek ? request.Content.Length : null);
    }

    public Task<Uri> CreateReadUrlAsync(
        string objectKey,
        TimeSpan expiresIn,
        CancellationToken cancellationToken = default)
    {
        var url = _publicS3.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _options.Bucket,
            Key = objectKey,
            Expires = DateTime.UtcNow.Add(expiresIn),
            Verb = HttpVerb.GET,
            Protocol = GetPublicUrlProtocol()
        });

        return Task.FromResult(new Uri(url));
    }

    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        return _s3.DeleteObjectAsync(_options.Bucket, objectKey, cancellationToken);
    }

    public async Task<MediaObjectMetadata?> GetMetadataAsync(
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _s3.GetObjectMetadataAsync(
                _options.Bucket,
                objectKey,
                cancellationToken);
            var metadata = response.Metadata.Keys
                .ToDictionary(
                    NormalizeMetadataKey,
                    key => response.Metadata[key],
                    StringComparer.OrdinalIgnoreCase);

            return new MediaObjectMetadata(
                objectKey,
                response.Headers.ContentType ?? "application/octet-stream",
                response.Headers.ContentLength,
                metadata);
        }
        catch (AmazonS3Exception ex) when (
            ex.StatusCode == HttpStatusCode.NotFound ||
            string.Equals(ex.ErrorCode, "NoSuchKey", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
    }

    private static string NormalizeMetadataKey(string key)
    {
        const string prefix = "x-amz-meta-";
        return key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? key[prefix.Length..]
            : key;
    }

    private static IAmazonS3 CreatePublicS3Client(S3Options options)
    {
        var publicServiceUrl = string.IsNullOrWhiteSpace(options.PublicUrl)
            ? options.ServiceUrl
            : options.PublicUrl;

        var config = new AmazonS3Config
        {
            ServiceURL = publicServiceUrl,
            ForcePathStyle = options.ForcePathStyle,
            AuthenticationRegion = options.Region,
            UseHttp = IsHttpUrl(publicServiceUrl)
        };

        return new AmazonS3Client(
            options.AccessKey,
            options.SecretKey,
            config);
    }

    private static bool IsHttpUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
            uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase);
    }

    private Protocol GetPublicUrlProtocol()
    {
        var publicServiceUrl = string.IsNullOrWhiteSpace(_options.PublicUrl)
            ? _options.ServiceUrl
            : _options.PublicUrl;

        return IsHttpUrl(publicServiceUrl)
            ? Protocol.HTTP
            : Protocol.HTTPS;
    }
}
