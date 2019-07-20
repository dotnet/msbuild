using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Microsoft.NET.Sdk.Publish.Tasks.ZipDeploy.Http
{ 
    public class HttpResponseMessageWrapper : IHttpResponse
    {
        private readonly HttpResponseMessage _message;
        private readonly Lazy<Task<Stream>> _responseBodyTask;

        public HttpResponseMessageWrapper(HttpResponseMessage message)
        {
            _message = message;
            StatusCode = message.StatusCode;
            _responseBodyTask = new Lazy<Task<Stream>>(GetResponseStream);
        }

        public HttpStatusCode StatusCode { get; private set; }

        public async Task<Stream> GetResponseBodyAsync()
        {
            return await _responseBodyTask.Value;
        }

        private Task<Stream> GetResponseStream()
        {
            return _message.Content.ReadAsStreamAsync();
        }

        public IEnumerable<string> GetHeader(string name)
        {
            IEnumerable<string> values;
            if (_message.Headers.TryGetValues(name, out values))
            {
                return values;
            }

            return new string[0];
        }
    }
}
