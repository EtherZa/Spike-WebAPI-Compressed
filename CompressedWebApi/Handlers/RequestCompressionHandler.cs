using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CompressedWebApi.Handlers
{
    public sealed class RequestCompressionHandler : DelegatingHandler
    {
        public enum EncodingType
        {
            /// <summary>
            /// Deflate compression.
            /// </summary>
            Deflate,

            /// <summary>
            /// gZip compression.
            /// </summary>
            Gzip
        }

        readonly Func<HttpContent, HttpContent> _factory;

        /// <summary>
        /// Initializes a new instance of <see cref="RequestCompressionHandler" />.
        /// </summary>
        /// <param name="encodingType">Encoding to use for request content.</param>
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

        /// <inheritdoc />
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CompressRequest(request);
            var response = await base.SendAsync(request, cancellationToken);
            DecompressResponseAsync(response);

            return response;
        }

        /// <summary>
        /// Compress request message content if required.
        /// </summary>
        /// <param name="request">Request message to compress.</param>
        void CompressRequest(HttpRequestMessage request)
        {
            if (request.Content != null)
            {
                request.Content = _factory(request.Content);
            }
        }

        /// <summary>
        /// Decompress response stream if required.
        /// </summary>
        /// <param name="response">Response stream to decompress.</param>
        static async void DecompressResponseAsync(HttpResponseMessage response)
        {
            var encoding = response.Content?.Headers.ContentEncoding;
            if (encoding == null || encoding.Any() == false)
            {
                return;
            }

            if (encoding.Count != 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(response.Content.Headers.ContentEncoding),
                    $"{nameof(RequestCompressionHandler)} does not support more than one encoding.");
            }

            var encodingType = encoding.Single().ToLowerInvariant();
            Stream stream;
            switch (encodingType)
            {
                case "deflate":
                    stream = new DeflateStream(await response.Content.ReadAsStreamAsync(), CompressionMode.Decompress);
                    break;

                case "gzip":
                    stream = new GZipStream(await response.Content.ReadAsStreamAsync(), CompressionMode.Decompress);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(response.Content.Headers.ContentEncoding), encodingType, "Encoding not supported.");
            }

            response.Content = new DecompressedContent(response.Content, stream);
        }
    }
}