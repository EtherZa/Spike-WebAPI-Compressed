using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CompressedWebApi.Handlers
{
    public class ServerCompressionHandler : DelegatingHandler
    {
        /// <inheritdoc />
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            DecompressRequestAsync(request);
            var response = await base.SendAsync(request, cancellationToken);
            CompressResponse(response);

            return response;
        }

        /// <summary>
        /// Compress the response as per requested encoding.
        /// </summary>
        /// <param name="response">Response message to compress.</param>
        static void CompressResponse(HttpResponseMessage response)
        {
            void ReplaceStream(string encoding, Func<Stream, Stream> factory)
            {
                response.Content = new CompressedContent(response.Content, encoding, factory);
            }

            if (response.Content == null
                || response.RequestMessage.Headers.AcceptEncoding == null
                || response.RequestMessage.Headers.AcceptEncoding.Any() == false)
            {
                return;
            }

            foreach (var encoding in response.RequestMessage.Headers.AcceptEncoding.Select(x => x.Value.ToLowerInvariant()))
            {
                switch (encoding)
                {
                    case "deflate":
                        ReplaceStream(encoding, s => new DeflateStream(s, CompressionMode.Compress, true));
                        return;

                    case "gzip":
                        ReplaceStream(encoding, s => new GZipStream(s, CompressionMode.Compress, true));
                        return;
                }
            }
        }

        /// <summary>
        /// Decompress request message if required.
        /// </summary>
        /// <param name="request">Request message to decompress.</param>
        static async void DecompressRequestAsync(HttpRequestMessage request)
        {
            var encoding = request.Content?.Headers.ContentEncoding;
            if (encoding == null || encoding.Any() == false)
            {
                return;
            }

            if (encoding.Count != 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(request.Content.Headers.ContentEncoding),
                    $"{nameof(ServerCompressionHandler)} does not support more than one encoding.");
            }

            var encodingType = encoding.Single().ToLowerInvariant();
            Stream stream;
            switch (encodingType)
            {
                case "deflate":
                    stream = new DeflateStream(await request.Content.ReadAsStreamAsync(), CompressionMode.Decompress);
                    break;

                case "gzip":
                    stream = new GZipStream(await request.Content.ReadAsStreamAsync(), CompressionMode.Decompress);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(request.Content.Headers.ContentEncoding), encodingType, "Encoding not supported.");
            }

            request.Content = new DecompressedContent(request.Content, stream);
        }
    }
}