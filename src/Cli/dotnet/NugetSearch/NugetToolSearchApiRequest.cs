// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Specialized;
using System.Web;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace Microsoft.DotNet.NugetSearch
{
    internal class NugetToolSearchApiRequest : INugetToolSearchApiRequest
    {
        public async Task<string> GetResult(NugetSearchApiParameter nugetSearchApiParameter)
        {
            var queryUrl = await ConstructUrl(
                nugetSearchApiParameter.SearchTerm,
                nugetSearchApiParameter.Skip,
                nugetSearchApiParameter.Take,
                nugetSearchApiParameter.Prerelease);

            var httpClient = new HttpClient();
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
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

        internal static async Task<Uri> ConstructUrl(string searchTerm = null, int? skip = null, int? take = null,
            bool prerelease = false, Uri domainAndPathOverride = null)
        {
            var uriBuilder = new UriBuilder(domainAndPathOverride ?? await DomainAndPath());
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

        // More detail on this API https://github.com/dotnet/sdk/issues/12038
        private static async Task<Uri> DomainAndPath()
        {
            var repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
            var resource = await repository.GetResourceAsync<ServiceIndexResourceV3>();
            var uris = resource.GetServiceEntryUris("SearchQueryService/3.5.0");
            return uris[0];
        }
    }
}
