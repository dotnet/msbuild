// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.NativeWrapper;
using System;
using System.Collections.Generic;
using System.CommandLine.Rendering;
using System.CommandLine.Rendering.Views;
using System.Linq;

namespace Microsoft.DotNet.Tools.CheckUpdate
{
    internal class RuntimeOutputFormatter : BundleOutputFormatter
    {
        private IEnumerable<NetRuntimeInfo> _runtimeInfo;

        public RuntimeOutputFormatter(
            IEnumerable<NetRuntimeInfo> runtimeInfo,
            ProductCollection productCollection,
            IProductCollectionProvider productCollectionProvider,
            IReporter reporter) : base(productCollection, productCollectionProvider, reporter)
        {
            _runtimeInfo = runtimeInfo;
        }

        public void PrintRuntimeInfo()
        {
            _reporter.WriteLine(LocalizableStrings.RuntimeSectionHeader);

            var runtimes = _runtimeInfo.OrderBy(runtime => runtime.Version);

            var grid = new GridView();
            grid.SetColumns(Enumerable.Repeat(ColumnDefinition.SizeToContent(), 3).ToArray());
            grid.SetRows(Enumerable.Repeat(RowDefinition.SizeToContent(), Math.Max(runtimes.Count(), 1)).ToArray());
            for (int i = 0; i < runtimes.Count(); i++)
            {
                var runtime = runtimes.ElementAt(i);
                grid.SetChild(new ContentView(string.Empty), 0, i);
                grid.SetChild(new ContentView($"{runtime.Name} {runtime.Version}"), 1, i);

                string runtimeMessage;
                if (BundleIsEndOfLife(runtime))
                {
                    runtimeMessage = string.Format(LocalizableStrings.OutOfSupportMessage, $"{runtime.Version.Major}.{runtime.Version.Minor}");
                }
                else if (BundleIsMaintenance(runtime))
                {
                    runtimeMessage = string.Format(LocalizableStrings.MaintenanceMessage, $"{runtime.Version.Major}.{runtime.Version.Minor}");
                }
                else if (NewerRuntimePatchExists(runtime))
                {
                    runtimeMessage = string.Format(LocalizableStrings.NewPatchAvaliableMessage, NewestRuntimePatchVersion(runtime));
                }
                else
                {
                    runtimeMessage = LocalizableStrings.BundleUpToDateMessage;
                }
                grid.SetChild(new ContentView(runtimeMessage), 2, i);
            }
            grid.Render(new ConsoleRenderer(new ReportingConsole(_reporter)), new Region(0, 0, int.MaxValue, int.MaxValue));
            _reporter.WriteLine();
        }

        private bool NewerRuntimePatchExists(NetRuntimeInfo bundle)
        {
            var newestPatchVesion = NewestRuntimePatchVersion(bundle);
            return !newestPatchVesion.Equals(bundle.Version);
        }

        private ReleaseVersion NewestRuntimePatchVersion(NetRuntimeInfo bundle)
        {
            var product = _productCollection.First(product => product.ProductVersion.Equals($"{bundle.Version.Major}.{bundle.Version.Minor}"));
            return product.LatestRuntimeVersion;
        }
    }
}
