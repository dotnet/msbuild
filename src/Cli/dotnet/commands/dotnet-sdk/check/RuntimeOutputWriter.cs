// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.NativeWrapper;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.Tools.Sdk.Check
{
    internal class RuntimeOutputWriter : BundleOutputWriter
    {
        private IEnumerable<NetRuntimeInfo> _runtimeInfo;

        public RuntimeOutputWriter(
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

            var table = new PrintableTable<NetRuntimeInfo>();
            table.AddColumn(LocalizableStrings.NameColumnHeader, runtime => runtime.Name.ToString());
            table.AddColumn(LocalizableStrings.VersionColumnHeader, runtime => runtime.Version.ToString());
            table.AddColumn(LocalizableStrings.StatusColumnHeader, runtime => GetRuntimeStatusMessage(runtime));

            table.PrintRows(_runtimeInfo.OrderBy(sdk => sdk.Version), l => _reporter.WriteLine(l));

            _reporter.WriteLine();
        }

        private string GetRuntimeStatusMessage(NetRuntimeInfo runtime)
        {
            if (BundleIsEndOfLife(runtime))
            {
                return string.Format(LocalizableStrings.OutOfSupportMessage, $"{runtime.Version.Major}.{runtime.Version.Minor}");
            }
            else if (BundleIsMaintenance(runtime))
            {
                return string.Format(LocalizableStrings.MaintenanceMessage, $"{runtime.Version.Major}.{runtime.Version.Minor}");
            }
            else if (NewerRuntimePatchExists(runtime))
            {
                return string.Format(LocalizableStrings.NewPatchAvailableMessage, NewestRuntimePatchVersion(runtime));
            }
            else
            {
                return LocalizableStrings.BundleUpToDateMessage;
            }
        }
        
        private bool NewerRuntimePatchExists(NetRuntimeInfo bundle)
        {
            var newestPatchVesion = NewestRuntimePatchVersion(bundle);
            return newestPatchVesion> bundle.Version;
        }

        private ReleaseVersion NewestRuntimePatchVersion(NetRuntimeInfo bundle)
        {
            var product = _productCollection.First(product => product.ProductVersion.Equals($"{bundle.Version.Major}.{bundle.Version.Minor}"));
            return product.LatestRuntimeVersion;
        }
    }
}
