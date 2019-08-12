// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.NET.HostModel;
using Microsoft.NET.HostModel.AppHost;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// Creates the runtime host to be used for an application.
    /// This embeds the application DLL path into the apphost and performs additional customizations as requested.
    /// </summary>
    public class CreateAppHost : TaskBase
    {
        /// <summary>
        /// The number of additional retries to attempt for creating the apphost.
        /// <summary>
        public const int DefaultRetries = 2;

        /// The default delay, in milliseconds, for each retry attempt for creating the apphost.
        /// </summary>
        public const int DefaultRetryDelayMilliseconds = 1000;

        [Required]
        public string AppHostSourcePath { get; set; }

        [Required]
        public string AppHostDestinationPath { get; set; }

        [Required]
        public string AppBinaryName { get; set; }

        [Required]
        public string IntermediateAssembly { get; set; }

        public bool WindowsGraphicalUserInterface { get; set; }

        public int Retries { get; set; } = DefaultRetries;

        public int RetryDelayMilliseconds { get; set; } = DefaultRetryDelayMilliseconds;

        protected override void ExecuteCore()
        {
            try
            {
                var isGUI = WindowsGraphicalUserInterface;
                var resourcesAssembly = IntermediateAssembly;

                if (!ResourceUpdater.IsSupportedOS())
                {
                    if (isGUI)
                    {
                        Log.LogWarning(Strings.AppHostCustomizationRequiresWindowsHostWarning);
                    }

                    isGUI = false;
                    resourcesAssembly = null;
                }

                int attempts = 0;
                
                while (true)
                {
                    try
                    {
                        HostWriter.CreateAppHost(appHostSourceFilePath: AppHostSourcePath,
                                                appHostDestinationFilePath: AppHostDestinationPath,
                                                appBinaryFilePath: AppBinaryName,
                                                windowsGraphicalUserInterface: isGUI,
                                                assemblyToCopyResorcesFrom: resourcesAssembly);
                        return;
                    }
                    catch (Exception ex) when (ex is IOException ||
                                               ex is UnauthorizedAccessException ||
                                               // Note: replace this when https://github.com/dotnet/core-setup/issues/7516 is fixed
                                               ex.GetType().Name == "HResultException")
                    {
                        if (Retries < 0 || attempts == Retries) {
                            throw;
                        }

                        ++attempts;

                        Log.LogWarning(
                            string.Format(Strings.AppHostCreationFailedWithRetry,
                                attempts,
                                Retries + 1,
                                ex.Message));

                        if (RetryDelayMilliseconds > 0) {
                            Thread.Sleep(RetryDelayMilliseconds);
                        }
                    }
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
