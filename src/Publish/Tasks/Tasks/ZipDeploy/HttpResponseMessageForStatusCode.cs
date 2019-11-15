using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace Microsoft.NET.Sdk.Publish.Tasks.ZipDeploy.Http
{
    internal class HttpResponseMessageForStatusCode : IHttpResponse
    {
        public HttpResponseMessageForStatusCode(HttpStatusCode statusCode)
        {
            StatusCode = statusCode;
        }

        public HttpStatusCode StatusCode { get; private set; }

        public Task<Stream> GetResponseBodyAsync()
        {
            return Task.FromResult<Stream>(new MemoryStream());
        }

        public IEnumerable<string> GetHeader(string name)
        {
            return new string[0];
        }
    }
}
