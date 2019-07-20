using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Microsoft.NET.Sdk.Publish.Tasks.ZipDeploy.Http
{
    public interface IHttpClient
    {
        HttpRequestHeaders DefaultRequestHeaders { get; }

        Task<HttpResponseMessage> PostAsync(Uri uri, StreamContent content);
    }
}
