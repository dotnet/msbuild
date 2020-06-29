// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using FluentAssertions;
using Microsoft.DotNet.NugetSearch;
using Xunit;

namespace dotnet.Tests.ToolSearchTests
{
    public class NugetSearchApiRequestTests
    {
        private readonly Uri _domainAndPathOverride = new Uri("https://azuresearch-usnc.nuget.org/query");

        [Fact]
        public void WhenPassedInRequestParametersItCanConstructTheUrl()
        {
            NugetToolSearchApiRequest.ConstructUrl("mytool", 3, 4, true, _domainAndPathOverride)
                .GetAwaiter().GetResult()
                .AbsoluteUri
                .Should().Be(
                    "https://azuresearch-usnc.nuget.org/query?q=mytool&packageType=dotnettool&semVerLevel=2.0.0&skip=3&take=4&prerelease=true");
        }

        [Fact]
        public void WhenPassedWithoutParameterItCanConstructTheUrl()
        {
            NugetToolSearchApiRequest.ConstructUrl(domainAndPathOverride:_domainAndPathOverride)
                .GetAwaiter().GetResult()
                .AbsoluteUri
                .Should().Be(
                    "https://azuresearch-usnc.nuget.org/query?packageType=dotnettool&semVerLevel=2.0.0");
        }
    }
}
