using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.NET.Sdk.Publish.Tasks.ZipDeploy.Http
{
    internal static class HttpClientHelpers
    {
        public static async Task<IHttpResponse> PostWithBasicAuthAsync(this IHttpClient client, Uri uri, string username, string password, string contentType, string userAgent, Encoding encoding, Stream messageBody)
        {
            AddBasicAuthToClient(username, password, client);
            client.DefaultRequestHeaders.Add("User-Agent", userAgent);

            StreamContent content = new StreamContent(messageBody ?? new MemoryStream())
            {
                Headers =
                {
                    ContentType = new MediaTypeHeaderValue(contentType)
                    {
                        CharSet = encoding.WebName
                    },
                    ContentEncoding =
                    {
                        encoding.WebName
                    }
                }
            };

            try
            {
                HttpResponseMessage responseMessage = await client.PostAsync(uri, content);
                return new HttpResponseMessageWrapper(responseMessage);
            }
            catch (TaskCanceledException)
            {
                return new HttpResponseMessageForStatusCode(HttpStatusCode.RequestTimeout);
            }
        }

        private static void AddBasicAuthToClient(string username, string password, IHttpClient client)
        {
            client.DefaultRequestHeaders.Remove("Connection");

            string plainAuth = string.Format("{0}:{1}", username, password);
            byte[] plainAuthBytes = Encoding.ASCII.GetBytes(plainAuth);
            string base64 = Convert.ToBase64String(plainAuthBytes);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64);
        }
    }
}
