// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Http;
using System.Net.Http.Headers;

namespace Microsoft.NET.Sdk.Publish.Tasks.ZipDeploy
{
    internal class DefaultHttpClient : IHttpClient, IDisposable
    {
        private readonly HttpClient _httpClient = new HttpClient()
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        public HttpRequestHeaders DefaultRequestHeaders => _httpClient.DefaultRequestHeaders;

        public void Dispose()
        {
            _httpClient.Dispose();
        }

        public Task<HttpResponseMessage> PostAsync(Uri uri, StreamContent content)
        {
            return _httpClient.PostAsync(uri, content);
        }

        public Task<HttpResponseMessage> GetAsync(Uri uri, CancellationToken cancellationToken)
        {
            return _httpClient.GetAsync(uri, cancellationToken);
        }
    }
}
