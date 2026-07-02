using System;
using System.Collections.Generic;
using System.Text;

namespace _808Music.Infrastructure.Storage
{
    public sealed class S3Options
    {
        public const string SectionName = "S3";

        public string ServiceUrl { get; init; } = string.Empty;
        public string AccessKey { get; init; } = string.Empty;
        public string SecretKey { get; init; } = string.Empty;
        public string Bucket { get; init; } = string.Empty;
        public string Region { get; init; } = "eu-central-1";
        public bool ForcePathStyle { get; init; } = true;
    }
}
