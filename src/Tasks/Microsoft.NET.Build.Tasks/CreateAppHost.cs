// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using System.IO;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// Creates the AppHost.exe to be used by the published app.
    /// This embeds the app dll path into the AppHost.exe and performs additional customizations as requested.
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
            var hostExtension = Path.GetExtension(AppHostSourcePath);
            var appbaseName = Path.GetFileNameWithoutExtension(AppBinaryName);

            if (!File.Exists(AppHostDestinationPath))
            {
                AppHost.Create(
                    AppHostSourcePath,
                    AppHostDestinationPath,
                    AppBinaryName,
                    windowsGraphicalUserInterface : WindowsGraphicalUserInterface,
                    intermediateAssembly: IntermediateAssembly,
                    log: Log);
            }
        }
    }
}
