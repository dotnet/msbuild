// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Tool.Search
{
    internal class SearchResultPrinter
    {
        private readonly IReporter _reporter;

        public SearchResultPrinter(IReporter reporter)
        {
            _reporter = reporter ?? throw new ArgumentNullException(nameof(reporter));
        }

        public void Print(bool isDetailed, IReadOnlyCollection<SearchResultPackage> searchResultPackages)
        {
            if (searchResultPackages.Count == 0)
            {
                _reporter.WriteLine(LocalizableStrings.NoResult);
                return;
            }

            if (!isDetailed)
            {
                var table = new PrintableTable<SearchResultPackage>();
                table.AddColumn(
                    LocalizableStrings.PackageId,
                    p => p.Id.ToString());
                table.AddColumn(
                    LocalizableStrings.LatestVersion,
                    p => p.LatestVersion);
                table.AddColumn(
                    LocalizableStrings.Authors,
                    p => p.Authors == null ? "" : string.Join(", ", p.Authors));
                table.AddColumn(
                    LocalizableStrings.Downloads,
                    p => p.TotalDownloads.ToString());
                table.AddColumn(
                    LocalizableStrings.Verified,
                    p => p.Verified ? "x" : "");

                table.PrintRows(searchResultPackages, l => _reporter.WriteLine(l));
            }
            else
            {
                foreach (var p in searchResultPackages)
                {
                    _reporter.WriteLine("----------------".Bold());
                    _reporter.WriteLine(p.Id.ToString());
                    _reporter.WriteLine($"{LocalizableStrings.LatestVersion}: ".Bold() + p.LatestVersion);
                    if (p.Authors != null)
                    {
                        _reporter.WriteLine($"{LocalizableStrings.Authors}: ".Bold() + string.Join(", ", p.Authors));
                    }

                    if (p.Tags != null)
                    {
                        _reporter.WriteLine($"{LocalizableStrings.Tags}: ".Bold() + string.Join(", ", p.Tags));
                    }

                    _reporter.WriteLine($"{LocalizableStrings.Downloads}: ".Bold() + p.TotalDownloads);


                    _reporter.WriteLine($"{LocalizableStrings.Verified}: ".Bold() + p.Verified.ToString());

                    if (!string.IsNullOrWhiteSpace(p.Summary))
                    {
                        _reporter.WriteLine($"{LocalizableStrings.Summary}: ".Bold() + p.Summary);
                    }

                    if (!string.IsNullOrWhiteSpace(p.Description))
                    {
                        _reporter.WriteLine($"{LocalizableStrings.Description}: ".Bold() + p.Description);
                    }

                    if (p.Versions.Count != 0)
                    {
                        _reporter.WriteLine($"{LocalizableStrings.Versions}: ".Bold());
                        foreach (SearchResultPackageVersion version in p.Versions)
                        {
                            _reporter.WriteLine(
                                $"\t{version.Version}" + $" {LocalizableStrings.Downloads}: ".Bold() + version.Downloads);
                        }
                    }

                    _reporter.WriteLine();
                }
            }
        }
    }
}
