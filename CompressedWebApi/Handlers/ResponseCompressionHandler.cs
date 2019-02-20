using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CompressedWebApi.Handlers
{
    public class ResponseCompressionHandler : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Decompress(request);
            var response = await base.SendAsync(request, cancellationToken);
            Compress(response);

            return response;
        }

        static void Compress(HttpResponseMessage response)
        {
            void ReplaceStream(string encoding, Func<Stream, Stream> factory)
            {
                response.Content = new CompressedContent(response.Content, encoding, factory);
            }

            if (response.Content == null
                || response.RequestMessage.Headers.AcceptEncoding == null)
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

            throw new ArgumentOutOfRangeException(nameof(response.RequestMessage.Headers.AcceptEncoding), "Encoding does not include any supported encodings.");
        }

        static async void Decompress(HttpRequestMessage request)
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
                    $"{nameof(ResponseCompressionHandler)} does not support more than one encoding.");
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

    sealed class CompressedContent : HttpContent
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

    sealed class DecompressedContent : StreamContent
    {
        public DecompressedContent(HttpContent content, Stream stream)
            : base(stream)
        {
            // copy the headers from the original content
            foreach (var header in content.Headers)
            {
                Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }
    }
}