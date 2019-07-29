// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.NET.HostModel;
using Microsoft.NET.HostModel.AppHost;
using System;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// Creates the runtime host to be used for an application.
    /// This embeds the application DLL path into the apphost and performs additional customizations as requested.
    /// </summary>
    public class CreateAppHost : TaskBase
    {
        [Required]
        public string AppHostSourcePath { get; set; }

        [Required]
        public string AppHostDestinationPath { get; set; }

        [Required]
        public string AppBinaryName { get; set; }

        [Required]
        public string IntermediateAssembly { get; set; }

        public bool WindowsGraphicalUserInterface { get; set; }

        protected override void ExecuteCore()
        {
            try
            {
                if (ResourceUpdater.IsSupportedOS())
                {
                    HostWriter.CreateAppHost(appHostSourceFilePath: AppHostSourcePath,
                                             appHostDestinationFilePath: AppHostDestinationPath,
                                             appBinaryFilePath: AppBinaryName,
                                             windowsGraphicalUserInterface: WindowsGraphicalUserInterface,
                                             assemblyToCopyResorcesFrom: IntermediateAssembly);
                }
                else
                {
                    // by passing null to assemblyToCopyResorcesFrom, it will skip copying resorces,
                    // which is only supported on Windows
                    if (WindowsGraphicalUserInterface)
                    {
                        Log.LogWarning(Strings.AppHostCustomizationRequiresWindowsHostWarning);
                    }

                    HostWriter.CreateAppHost(appHostSourceFilePath: AppHostSourcePath,
                                             appHostDestinationFilePath: AppHostDestinationPath,
                                             appBinaryFilePath: AppBinaryName,
                                             windowsGraphicalUserInterface: false,
                                             assemblyToCopyResorcesFrom: null);

                }
            }
            catch (AppNameTooLongException ex)
            {
                throw new BuildErrorException(Strings.FileNameIsTooLong, ex.LongName);
            }
            catch (PlaceHolderNotFoundInAppHostException ex)
            {
                throw new BuildErrorException(Strings.AppHostHasBeenModified, AppHostSourcePath, BitConverter.ToString(ex.MissingPattern));
            }
        }
    }
}
