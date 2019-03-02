using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace CompressedWebApi.Handlers
{
    public sealed class ClientCompressionHandler : DelegatingHandler
    {
        static readonly IReadOnlyDictionary<ContentEncoding, Func<HttpContent, HttpContent>> CompressionHandlers;
        static readonly IReadOnlyDictionary<string, Func<Stream, Stream>> DecompressionHandler;

        readonly Func<HttpContent, HttpContent> _factory;

        /// <summary>
        /// Initializes static variables.
        /// </summary>
        static ClientCompressionHandler()
        {
            CompressionHandlers = new Dictionary<ContentEncoding, Func<HttpContent, HttpContent>>
            {
                { ContentEncoding.Deflate, c => new CompressedContent(c, "deflate", s => new DeflateStream(s, CompressionMode.Compress, true)) },
                { ContentEncoding.Gzip, c => new CompressedContent(c, "gzip", s => new GZipStream(s, CompressionMode.Compress, true)) }
            };

            DecompressionHandler = new Dictionary<string, Func<Stream, Stream>>(StringComparer.OrdinalIgnoreCase)
            {
                { "gzip", s => new GZipStream(s, CompressionMode.Decompress) },
                { "deflate", s => new DeflateStream(s, CompressionMode.Decompress) }
            };
        }

        /// <summary>
        /// Initializes a new instance of <see cref="ClientCompressionHandler" />.
        /// </summary>
        /// <param name="supportedContentEncoding">Content encoding supported by the server.</param>
        public ClientCompressionHandler(params ContentEncoding[] supportedContentEncoding)
        {
            foreach (var encoding in supportedContentEncoding)
            {
                if (CompressionHandlers.TryGetValue(encoding, out var factory))
                {
                    _factory = factory;
                    return;
                }
            }

            _factory = x => x;
        }

        /// <inheritdoc />
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            foreach (var encoding in DecompressionHandler.Keys)
            {
                request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue(encoding));
            }

            CompressRequest(request);
            var response = await base.SendAsync(request, cancellationToken);
            await DecompressResponseAsync(response);

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
        static async Task DecompressResponseAsync(HttpResponseMessage response)
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
                    $"{nameof(ClientCompressionHandler)} does not support more than one encoding.");
            }

            var encodingType = encoding.Single();
            if (DecompressionHandler.TryGetValue(encodingType, out var factory))
            {
                response.Content = new DecompressedContent(response.Content, factory(await response.Content.ReadAsStreamAsync()));
                return;
            }

            throw new ArgumentOutOfRangeException(nameof(response.Content.Headers.ContentEncoding), encodingType, "Encoding not supported.");
        }
    }
}