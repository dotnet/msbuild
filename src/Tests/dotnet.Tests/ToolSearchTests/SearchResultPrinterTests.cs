// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Tools.Tool.Search;

namespace dotnet.Tests.ToolSearchTests
{
    public class SearchResultPrinterTests
    {
        private readonly BufferedReporter _reporter;
        private readonly SearchResultPrinter _searchResultPrinter;
        private readonly SearchResultPackage _filledSearchResultPackage;
        private readonly SearchResultPackage _mostEmptyToCheckNullException;

        public SearchResultPrinterTests()
        {
            _reporter = new BufferedReporter();
            _searchResultPrinter = new SearchResultPrinter(_reporter);

            _filledSearchResultPackage = new SearchResultPackage(
                new PackageId("my.tool"),
                "1.0.0",
                "my tool description",
                "my tool summary",
                new List<string> {"tag1", "tag2"},
                new List<string> {"author1", "author2"},
                10,
                true,
                new List<SearchResultPackageVersion> {new SearchResultPackageVersion("1.0.0", 10)});
            _mostEmptyToCheckNullException = new SearchResultPackage(
                new PackageId("my.tool"),
                "1.0.0",
                null,
                null,
                new List<string>(),
                new List<string> {"author1", "author2"},
                1244,
                true,
                new List<SearchResultPackageVersion> {new SearchResultPackageVersion("1.0.0", 10), new SearchResultPackageVersion("0.9.0", 1234)});
        }

        [Fact]
        public void WhenDetailedIsFalseResultHasNecessaryInfo()
        {
            var searchResultPackages =
                new List<SearchResultPackage> {_filledSearchResultPackage, _mostEmptyToCheckNullException};
            _searchResultPrinter.Print(false, searchResultPackages);

            string[] expectedInformation =
            {
                _filledSearchResultPackage.Id.ToString(), _filledSearchResultPackage.Authors.First(),
                _filledSearchResultPackage.TotalDownloads.ToString(),
                _filledSearchResultPackage.Versions.First().Version,
                _filledSearchResultPackage.Versions.First().Downloads.ToString(),
                _mostEmptyToCheckNullException.Id.ToString()
            };

            foreach (var expectedInformationToBePresent in expectedInformation)
                _reporter.Lines.Should().Contain(l => l.Contains(expectedInformationToBePresent),
                    $"Expect \"{expectedInformationToBePresent}\" to be present");

            _reporter.Lines.Should().NotContain(l => l.Contains(_filledSearchResultPackage.Description));
            _reporter.Lines.Should().NotContain(l => l.Contains(_filledSearchResultPackage.Summary));
            _reporter.Lines.Should().NotContain(l => l.Contains(_filledSearchResultPackage.Tags.First()));
        }

        [Fact]
        public void WhenDetailedIsTrueResultHasNecessaryInfo()
        {
            var searchResultPackages =
                new List<SearchResultPackage> {_filledSearchResultPackage, _mostEmptyToCheckNullException};
            _searchResultPrinter.Print(true, searchResultPackages);

            string[] expectedInformation =
            {
                _filledSearchResultPackage.Id.ToString(), _filledSearchResultPackage.Authors.First(),
                _filledSearchResultPackage.TotalDownloads.ToString(),
                _filledSearchResultPackage.Versions.First().Version,
                _filledSearchResultPackage.Versions.First().Downloads.ToString(),
                _mostEmptyToCheckNullException.Id.ToString(), _filledSearchResultPackage.Description,
                _filledSearchResultPackage.Summary, _filledSearchResultPackage.Tags.First(),
                _filledSearchResultPackage.Versions.First().Version,
                _filledSearchResultPackage.Versions.First().Downloads.ToString(),
            };

            foreach (var expectedInformationToBePresent in expectedInformation)
                _reporter.Lines.Should().Contain(l => l.Contains(expectedInformationToBePresent),
                    $"Expect \"{expectedInformationToBePresent}\" to be present");
        }

        [Fact]
        public void WhenInputIsEmptyDetailIsFalseItShouldPrintNoResultMessage()
        {
            var searchResultPackages =
                new List<SearchResultPackage>() ;
            _searchResultPrinter.Print(false, searchResultPackages);
            _reporter.Lines.Count.Should().Be(1);
            _reporter.Lines.Should().Contain(LocalizableStrings.NoResult);
        }
        
        [Fact]
        public void WhenInputIsEmptyDetailIsTrueItShouldPrintNoResultMessage()
        {
            var searchResultPackages =
                new List<SearchResultPackage>() ;
            _searchResultPrinter.Print(true, searchResultPackages);
            _reporter.Lines.Count.Should().Be(1);
            _reporter.Lines.Should().Contain(LocalizableStrings.NoResult);
        }
    }
}
