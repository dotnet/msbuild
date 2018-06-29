// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.PlatformAbstractions;
using Microsoft.DotNet.Tools.Common;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.ShellShim
{
    internal class AppHostShellShimMaker : IAppHostShellShimMaker
    {
        private const string ApphostNameWithoutExtension = "apphost";
        private readonly string _appHostSourceDirectory;
        private readonly IFilePermissionSetter _filePermissionSetter;

        public AppHostShellShimMaker(string appHostSourceDirectory = null, IFilePermissionSetter filePermissionSetter = null)
        {
            _appHostSourceDirectory =
                appHostSourceDirectory
                ?? Path.Combine(ApplicationEnvironment.ApplicationBasePath, "AppHostTemplate");

            _filePermissionSetter =
                filePermissionSetter
                ?? new FilePermissionSetter();
        }

        public void CreateApphostShellShim(FilePath entryPoint, FilePath shimPath)
        {
            string appHostSourcePath;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                appHostSourcePath = Path.Combine(_appHostSourceDirectory, ApphostNameWithoutExtension + ".exe");
            }
            else
            {
                appHostSourcePath = Path.Combine(_appHostSourceDirectory, ApphostNameWithoutExtension);
            }

            var appHostDestinationFilePath = Path.GetFullPath(shimPath.Value);
            var appBinaryFilePath = Path.GetRelativePath(Path.GetDirectoryName(appHostDestinationFilePath), Path.GetFullPath(entryPoint.Value));

            EmbedAppNameInHost.EmbedAndReturnModifiedAppHostPath(
                appHostSourceFilePath: appHostSourcePath,
                appHostDestinationFilePath: appHostDestinationFilePath,
                appBinaryFilePath: appBinaryFilePath);

            _filePermissionSetter.SetUserExecutionPermission(appHostDestinationFilePath);
        }
    }
}
