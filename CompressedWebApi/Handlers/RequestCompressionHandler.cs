using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CompressedWebApi.Handlers
{
    public sealed class RequestCompressionHandler : DelegatingHandler
    {
        public enum EncodingType
        {
            Deflate,
            Gzip
        }

        readonly Func<HttpContent, HttpContent> _factory;

        public RequestCompressionHandler(EncodingType encodingType = EncodingType.Gzip)
        {
            switch (encodingType)
            {
                case EncodingType.Deflate:
                    _factory = c => new CompressedContent(c, "deflate", s => new DeflateStream(s, CompressionMode.Compress, true));
                    break;

                case EncodingType.Gzip:
                    _factory = c => new CompressedContent(c, "gzip", s => new GZipStream(s, CompressionMode.Compress, true));
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(encodingType), encodingType, "Encoding not supported.");
            }
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content != null)
            {
                request.Content = _factory(request.Content);
            }

            return base.SendAsync(request, cancellationToken);
        }

        class CompressedContent : HttpContent
        {
            readonly HttpContent _content;
            readonly Func<Stream, Stream> _factory;

            public CompressedContent(HttpContent content, string encodingType, Func<Stream, Stream> factory)
            {
                _content = content;
                _factory = factory;

                foreach (var header in content.Headers)
                {
                    Headers.TryAddWithoutValidation(header.Key, header.Value);
                }

                Headers.ContentEncoding.Add(encodingType);
            }

            protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context)
            {
                using (var compressedStream = _factory(stream))
                {
                    await _content.CopyToAsync(compressedStream);
                }
            }

            protected override bool TryComputeLength(out long length)
            {
                length = -1;
                return false;
            }
        }
    }
}