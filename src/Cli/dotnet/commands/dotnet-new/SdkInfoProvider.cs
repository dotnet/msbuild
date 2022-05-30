// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.NativeWrapper;

namespace Microsoft.DotNet.Tools.New
{
    internal class SdkInfoProvider : ISdkInfoProvider
    {
        public Guid Id { get; } = Guid.Parse("{A846C4E2-1E85-4BF5-954D-17655D916928}");

        public Task<string> GetCurrentVersionAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(Product.Version);
        }

        public Task<IEnumerable<string>> GetInstalledVersionsAsync(CancellationToken cancellationToken)
        {
            string dotnetDir = Microsoft.DotNet.NativeWrapper.EnvironmentProvider.GetDotnetExeDirectory();
            string[] sdks = NETCoreSdkResolverNativeWrapper.GetAvailableSdks(dotnetDir);
            return Task.FromResult((IEnumerable<string>)sdks);
        }

        public string ProvideConstraintRemedySuggestion(IReadOnlyList<string> supportedVersions,
            IReadOnlyList<string> viableInstalledVersions)
        {
            if (viableInstalledVersions.Any())
            {
                return string.Format(LocalizableStrings.SdkInfoProvider_Message_SwitchSdk, viableInstalledVersions.ToCsvString());
            }
            else
            {
                return string.Format(LocalizableStrings.SdkInfoProvider_Message_InstallSdk, supportedVersions.ToCsvString());
            }
        }
    }
}
