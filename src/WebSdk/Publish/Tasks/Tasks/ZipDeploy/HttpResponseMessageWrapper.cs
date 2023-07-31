// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Net.Http;

namespace Microsoft.NET.Sdk.Publish.Tasks.ZipDeploy
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
