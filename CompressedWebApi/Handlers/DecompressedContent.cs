using System;
using System.IO;
using System.Linq;
using System.Net.Http;

namespace CompressedWebApi.Handlers
{
    sealed class DecompressedContent : StreamContent
    {
        public DecompressedContent(HttpContent content, Stream stream)
            : base(stream)
        {
            // copy the headers from the original content
            foreach (var header in content.Headers.Where(x => x.Key.Equals("Content-Encoding", StringComparison.OrdinalIgnoreCase) == false))
            {
                Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }
    }
}