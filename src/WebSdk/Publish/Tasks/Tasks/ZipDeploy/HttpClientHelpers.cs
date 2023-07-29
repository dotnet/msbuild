// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

namespace Microsoft.NET.Sdk.Publish.Tasks.ZipDeploy
{
    internal static class HttpClientHelpers
    {
        internal static readonly string AzureADUserName = Guid.Empty.ToString();
        internal static readonly string BearerAuthenticationScheme = "Bearer";
        internal static readonly string BasicAuthenticationScheme = "Basic";
        public static async Task<IHttpResponse> PostRequestAsync(this IHttpClient client, Uri uri, string username, string password, string contentType, string userAgent, Encoding encoding, Stream messageBody)
        {
            AddAuthenticationHeader(username, password, client);
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

        public static async Task<IHttpResponse> GetRequestAsync(this IHttpClient client, Uri uri, string username, string password, string userAgent, CancellationToken cancellationToken)
        {
            AddAuthenticationHeader(username, password, client);
            client.DefaultRequestHeaders.Add("User-Agent", userAgent);

            try
            {
                HttpResponseMessage responseMessage = await client.GetAsync(uri, cancellationToken);
                return new HttpResponseMessageWrapper(responseMessage);
            }
            catch (TaskCanceledException)
            {
                return new HttpResponseMessageForStatusCode(HttpStatusCode.RequestTimeout);
            }
        }

        private static void AddAuthenticationHeader(string username, string password, IHttpClient client)
        {
            client.DefaultRequestHeaders.Remove("Connection");

            if (!string.Equals(username, AzureADUserName, StringComparison.Ordinal))
            {
                string plainAuth = string.Format("{0}:{1}", username, password);
                byte[] plainAuthBytes = Encoding.ASCII.GetBytes(plainAuth);
                string base64 = Convert.ToBase64String(plainAuthBytes);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(BasicAuthenticationScheme, base64);
            }
            else
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(BearerAuthenticationScheme, password);
            }
        }
    }
}
