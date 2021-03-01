// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.NativeWrapper;
using System.Collections.Generic;
using System.Linq;
using System.CommandLine.Rendering;
using System.CommandLine.Rendering.Views;
using System.CommandLine;
using System.CommandLine.IO;
using System;

namespace Microsoft.DotNet.Tools.CheckUpdate
{
    internal class SdkOutputFormatter : BundleOutputFormatter
    {
        private IEnumerable<NetSdkInfo> _sdkInfo;

        public SdkOutputFormatter(
            IEnumerable<NetSdkInfo> sdkInfo,
            ProductCollection productCollection,
            IProductCollectionProvider productCollectionProvider,
            IReporter reporter) : base(productCollection, productCollectionProvider, reporter)
        {
            _sdkInfo = sdkInfo;
        }

        public void PrintSdkInfo()
        {
            _reporter.WriteLine(LocalizableStrings.SdkSectionHeader);

            var sdks = _sdkInfo.OrderBy(sdk => sdk.Version);

            var grid = new GridView();
            grid.SetColumns(Enumerable.Repeat(ColumnDefinition.SizeToContent(), 3).ToArray());
            grid.SetRows(Enumerable.Repeat(RowDefinition.SizeToContent(), Math.Max(sdks.Count(), 1)).ToArray());
            for (int i = 0; i < sdks.Count(); i++)
            {
                var sdk = sdks.ElementAt(i);
                grid.SetChild(new ContentView(string.Empty), 0, i);
                grid.SetChild(new ContentView(sdk.Version.ToString()), 1, i);

                string sdkMessage;
                if (BundleIsEndOfLife(sdk))
                {
                    sdkMessage = string.Format(LocalizableStrings.OutOfSupportMessage, $"{sdk.Version.Major}.{sdk.Version.Minor}");
                }
                else if (BundleIsMaintenance(sdk))
                {
                    sdkMessage = string.Format(LocalizableStrings.MaintenanceMessage, $"{sdk.Version.Major}.{sdk.Version.Minor}");
                }
                else if (NewerSdkPatchExists(sdk))
                {
                    sdkMessage = string.Format(LocalizableStrings.NewPatchAvaliableMessage, NewestSdkPatchVersion(sdk));
                }
                else
                {
                    sdkMessage = LocalizableStrings.BundleUpToDateMessage;
                }
                grid.SetChild(new ContentView(sdkMessage), 2, i);
            }
            grid.Render(new ConsoleRenderer(new ReportingConsole(_reporter)), new Region(0, 0, int.MaxValue, int.MaxValue));
            _reporter.WriteLine();

            if (NewFeatureBandAvaliable())
            {
                _reporter.WriteLine();
                _reporter.WriteLine(string.Format(LocalizableStrings.NewFeatureBandMessage, NewestFeatureBandAvaliable()));
            }
        }

        private bool NewerSdkPatchExists(NetSdkInfo bundle)
        {
            var newestPatchVesion = NewestSdkPatchVersion(bundle);
            return newestPatchVesion == null ? false : !newestPatchVesion.Equals(bundle.Version);
        }

        private ReleaseVersion NewestSdkPatchVersion(NetSdkInfo bundle)
        {
            var product = _productCollection.First(product => product.ProductVersion.Equals($"{bundle.Version.Major}.{bundle.Version.Minor}"));
            if (product.LatestSdkVersion.SdkFeatureBand == bundle.Version.SdkFeatureBand)
            {
                // This is the latest feature band
                return product.LatestSdkVersion;
            }
            else
            {
                // Fetch detailed product release information
                var productReleases = _productCollectionProvider.GetProductReleases(product);
                var featureBandVersions = productReleases
                    .SelectMany(release => release.Sdks)
                    .Select(sdk => sdk.Version)
                    .Where(sdkVersion => sdkVersion.SdkFeatureBand == bundle.Version.SdkFeatureBand);
                return featureBandVersions.FirstOrDefault();
            }
        }

        private bool NewFeatureBandAvaliable()
        {
            return NewestFeatureBandAvaliable() > _sdkInfo.Select(sdk => sdk.Version).Max();
        }

        private ReleaseVersion NewestFeatureBandAvaliable()
        {
            return _productCollection.OrderByDescending(product => product.ProductVersion).First().LatestSdkVersion;
        }
    }
}
