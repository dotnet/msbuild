// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.NativeWrapper;

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
            bool? endOfLife = BundleIsEndOfLife(runtime);
            bool? isMaintenance = BundleIsMaintenance(runtime);
            bool? runtimePatchExists = NewerRuntimePatchExists(runtime);
            if (endOfLife == true)
            {
                return string.Format(LocalizableStrings.OutOfSupportMessage, $"{runtime.Version.Major}.{runtime.Version.Minor}");
            }
            else if (isMaintenance == true)
            {
                return string.Format(LocalizableStrings.MaintenanceMessage, $"{runtime.Version.Major}.{runtime.Version.Minor}");
            }
            else if (runtimePatchExists == true)
            {
                return string.Format(LocalizableStrings.NewPatchAvailableMessage, NewestRuntimePatchVersion(runtime));
            }
            else if (endOfLife == false && isMaintenance == false && runtimePatchExists == false)
            {
                return LocalizableStrings.BundleUpToDateMessage;
            }
            else
            {
                return LocalizableStrings.VersionCheckFailure;
            }
        }

        private bool? NewerRuntimePatchExists(NetRuntimeInfo bundle)
        {
            var newestPatchVesion = NewestRuntimePatchVersion(bundle);
            if (newestPatchVesion == null)
            {
                return null;
            }

            return newestPatchVesion > bundle.Version;
        }

        private ReleaseVersion? NewestRuntimePatchVersion(NetRuntimeInfo bundle)
        {
            var product = _productCollection.FirstOrDefault(product => product.ProductVersion.Equals($"{bundle.Version.Major}.{bundle.Version.Minor}"));
            return product?.LatestRuntimeVersion;
        }
    }
}
