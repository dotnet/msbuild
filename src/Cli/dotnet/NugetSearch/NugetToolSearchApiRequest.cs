// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Specialized;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Microsoft.DotNet.NugetSearch
{
    internal class NugetToolSearchApiRequest : INugetToolSearchApiRequest
    {
        public async Task<string> GetResult(NugetSearchApiParameter nugetSearchApiParameter)
        {
            var queryUrl = ConstructUrl(
                nugetSearchApiParameter.SearchTerm,
                nugetSearchApiParameter.Skip,
                nugetSearchApiParameter.Take,
                nugetSearchApiParameter.Prerelease);

            var httpClient = new HttpClient();
            var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            HttpResponseMessage response = await httpClient.GetAsync(queryUrl, cancellation.Token);
            if (!response.IsSuccessStatusCode)
            {
                if ((int)response.StatusCode >= 500 && (int)response.StatusCode < 600)
                {
                    throw new NugetSearchApiRequestException(
                        string.Format(
                            LocalizableStrings.RetriableNugetSearchFailure,
                            queryUrl.AbsoluteUri, response.ReasonPhrase, response.StatusCode));
                }

                throw new NugetSearchApiRequestException(
                    string.Format(
                        LocalizableStrings.NonRetriableNugetSearchFailure,
                        queryUrl.AbsoluteUri, response.ReasonPhrase, response.StatusCode));
            }

            return await response.Content.ReadAsStringAsync(cancellation.Token);
        }

        internal static Uri ConstructUrl(string searchTerm = null, int? skip = null, int? take = null,
            bool prerelease = false)
        {
            var uriBuilder = new UriBuilder("https://azuresearch-usnc.nuget.org/query");
            NameValueCollection query = HttpUtility.ParseQueryString(uriBuilder.Query);
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query["q"] = searchTerm;
            }

            query["packageType"] = "dotnettool";

            // This is a field for internal nuget back
            // compactabiliy should be "2.0.0" for all new API usage
            query["semVerLevel"] = "2.0.0";

            if (skip.HasValue)
            {
                query["skip"] = skip.Value.ToString();
            }

            if (take.HasValue)
            {
                query["take"] = take.Value.ToString();
            }

            if (prerelease)
            {
                query["prerelease"] = "true";
            }

            uriBuilder.Query = query.ToString();

            return uriBuilder.Uri;
        }
    }
}
