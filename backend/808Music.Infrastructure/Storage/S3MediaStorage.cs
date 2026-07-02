using _808Music.Application.Abstractions;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace _808Music.Infrastructure.Storage;

public sealed class S3MediaStorage : IMediaStorage
{
    private readonly IAmazonS3 _s3;
    private readonly S3Options _options;

    public S3MediaStorage(IAmazonS3 s3, IOptions<S3Options> options)
    {
        _s3 = s3;
        _options = options.Value;
    }

    public async Task<StoredMediaObject> UploadAsync(
        UploadMediaObject request,
        CancellationToken cancellationToken = default)
    {
        await _s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _options.Bucket,
            Key = request.ObjectKey,
            InputStream = request.Content,
            ContentType = request.ContentType
        }, cancellationToken);

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
        var url = _s3.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _options.Bucket,
            Key = objectKey,
            Expires = DateTime.UtcNow.Add(expiresIn),
            Verb = HttpVerb.GET
        });

        return Task.FromResult(new Uri(url));
    }

    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        return _s3.DeleteObjectAsync(_options.Bucket, objectKey, cancellationToken);
    }
}
