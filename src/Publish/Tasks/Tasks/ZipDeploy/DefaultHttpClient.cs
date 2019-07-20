using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Microsoft.NET.Sdk.Publish.Tasks.ZipDeploy.Http
{
    internal class DefaultHttpClient : IHttpClient, IDisposable
    {
        private HttpClient _httpClient = new HttpClient();

        public HttpRequestHeaders DefaultRequestHeaders => _httpClient.DefaultRequestHeaders;

        public void Dispose()
        {
            _httpClient.Dispose();
        }

        public Task<HttpResponseMessage> PostAsync(Uri uri, StreamContent content)
        {
            return _httpClient.PostAsync(uri, content);
        }
    }
}
