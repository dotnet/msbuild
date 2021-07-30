// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Tools;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.HostModel;
using Microsoft.NET.HostModel.AppHost;

namespace Microsoft.DotNet.ShellShim
{
    internal class AppHostShellShimMaker : IAppHostShellShimMaker
    {
        private const string ApphostNameWithoutExtension = "apphost";
        private readonly string _appHostSourceDirectory;
        private readonly IFilePermissionSetter _filePermissionSetter;
        private const ushort WindowsGUISubsystem = 0x2;

        public AppHostShellShimMaker(string appHostSourceDirectory, IFilePermissionSetter filePermissionSetter = null)
        {
            _appHostSourceDirectory = appHostSourceDirectory;

            _filePermissionSetter =
                filePermissionSetter
                ?? new FilePermissionSetter();
        }

        public void CreateApphostShellShim(FilePath entryPoint, FilePath shimPath)
        {
            string appHostSourcePath;
            if (OperatingSystem.IsWindows())
            {
                appHostSourcePath = Path.Combine(_appHostSourceDirectory, ApphostNameWithoutExtension + ".exe");
            }
            else
            {
                appHostSourcePath = Path.Combine(_appHostSourceDirectory, ApphostNameWithoutExtension);
            }

            var appHostDestinationFilePath = Path.GetFullPath(shimPath.Value);
            string entryPointFullPath = Path.GetFullPath(entryPoint.Value);
            var appBinaryFilePath = Path.GetRelativePath(Path.GetDirectoryName(appHostDestinationFilePath), entryPointFullPath);


            if (ResourceUpdater.IsSupportedOS())
            {
                var windowsGraphicalUserInterfaceBit = PEUtils.GetWindowsGraphicalUserInterfaceBit(entryPointFullPath);
                HostWriter.CreateAppHost(appHostSourceFilePath: appHostSourcePath,
                                         appHostDestinationFilePath: appHostDestinationFilePath,
                                         appBinaryFilePath: appBinaryFilePath,
                                         windowsGraphicalUserInterface: (windowsGraphicalUserInterfaceBit == WindowsGUISubsystem),
                                         assemblyToCopyResorcesFrom: entryPointFullPath);
            }
            else
            {
                // by passing null to assemblyToCopyResorcesFrom, it will skip copying resources,
                // which is only supported on Windows
                HostWriter.CreateAppHost(appHostSourceFilePath: appHostSourcePath,
                                         appHostDestinationFilePath: appHostDestinationFilePath,
                                         appBinaryFilePath: appBinaryFilePath,
                                         windowsGraphicalUserInterface: false,
                                         assemblyToCopyResorcesFrom: null);
            }

            _filePermissionSetter.SetUserExecutionPermission(appHostDestinationFilePath);
        }
    }
}
