using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace CompressedWebApi.Handlers
{
    sealed class CompressedContent : HttpContent
    {
        readonly HttpContent _content;
        readonly Func<Stream, Stream> _factory;

        /// <summary>
        /// Initializes a new instance of <see cref="CompressedContent" />.
        /// </summary>
        /// <param name="content">Content to compress.</param>
        /// <param name="encodingType">Encoding type to be specified.</param>
        /// <param name="factory">Compression stream factory.</param>
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

        /// <inheritdoc />
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            using (var compressedStream = _factory(stream))
            {
                await _content.CopyToAsync(compressedStream);
            }
        }

        /// <inheritdoc />
        protected override bool TryComputeLength(out long length)
        {
            length = -1;
            return false;
        }
    }
}