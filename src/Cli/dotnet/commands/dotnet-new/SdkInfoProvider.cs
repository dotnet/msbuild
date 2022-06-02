// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli;
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
            var dotnetDir = CommonOptions.GetDotnetExeDirectory();
            // sdks contain the full path, version is the last part
            //  details: https://github.com/dotnet/runtime/blob/5098d45cc1bf9649fab5df21f227da4b80daa084/src/native/corehost/fxr/sdk_info.cpp#L76
            IEnumerable<string> sdks = NETCoreSdkResolverNativeWrapper.GetAvailableSdks(dotnetDir).Select(Path.GetFileName);
            return Task.FromResult(sdks);
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
