// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;

namespace Microsoft.NET.Sdk.Publish.Tasks.ZipDeploy
{
    internal class HttpResponseMessageForStatusCode : IHttpResponse
    {
        public HttpResponseMessageForStatusCode(HttpStatusCode statusCode)
        {
            StatusCode = statusCode;
        }

        public HttpStatusCode StatusCode { get; private set; }

        public System.Threading.Tasks.Task<Stream> GetResponseBodyAsync()
        {
            return System.Threading.Tasks.Task.FromResult<Stream>(new MemoryStream());
        }

        public IEnumerable<string> GetHeader(string name)
        {
            return new string[0];
        }
    }
}
