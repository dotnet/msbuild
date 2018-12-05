// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

using UtilitiesProcessorArchitecture = Microsoft.Build.Utilities.ProcessorArchitecture;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Main class for the COM reference resolution task
    /// </summary>
    public sealed partial class ResolveComReference : Microsoft.Build.Tasks.TaskExtension
    {
        #region Properties

        /// <summary>
        /// COM references specified by guid/version/lcid
        /// </summary>
        public ITaskItem[] TypeLibNames { get; set; }

        /// <summary>
        /// COM references specified by type library file path
        /// </summary>
        public ITaskItem[] TypeLibFiles { get; set; }

        /// <summary>
        /// Array of equals-separated pairs of environment
        /// variables that should be passed to the spawned tlbimp.exe and aximp.exe,
        /// in addition to (or selectively overriding) the regular environment block.
        /// </summary>
        public string[] EnvironmentVariables { get; set; }

        /// <summary>
        /// the directory wrapper files get generated into
        /// </summary>
        public string WrapperOutputDirectory { get; set; }

        /// <summary>
        /// When set to true, the typelib version will be included in the wrapper name.  Default is false.
        /// </summary>
        public bool IncludeVersionInInteropName { get; set; }

        /// <summary>
        /// source of resolved .NET assemblies - we need this for ActiveX wrappers, since we can't resolve .NET assembly
        /// references ourselves
        /// </summary>
        public ITaskItem[] ResolvedAssemblyReferences { get; set; }

        /// <summary>
        /// container name for public/private keys
        /// </summary>
        public string KeyContainer { get; set; } 

        /// <summary>
        /// file containing public/private keys
        /// </summary>
        public string KeyFile { get; set; }

        /// <summary>
        /// delay sign wrappers?
        /// </summary>
        public bool DelaySign { get; set; }

        /// <summary>
        /// Passes the TypeLibImporterFlags.PreventClassMembers flag to tlb wrapper generation
        /// </summary>
        public bool NoClassMembers { get; set; } 

        /// <summary>
        /// If true, do not log messages or warnings.  Default is false. 
        /// </summary>
        public bool Silent { get; set; }

        /// <summary>
        /// The preferred target processor architecture. Passed to tlbimp.exe /machine flag after translation. 
        /// Should be a member of Microsoft.Build.Utilities.ProcessorArchitecture.
        /// </summary>
        public string TargetProcessorArchitecture
        {
            get => _targetProcessorArchitecture;

            set
            {
                if (UtilitiesProcessorArchitecture.X86.Equals(value, StringComparison.OrdinalIgnoreCase))
                {
                    _targetProcessorArchitecture = UtilitiesProcessorArchitecture.X86;
                }
                else if (UtilitiesProcessorArchitecture.MSIL.Equals(value, StringComparison.OrdinalIgnoreCase))
                {
                    _targetProcessorArchitecture = UtilitiesProcessorArchitecture.MSIL;
                }
                else if (UtilitiesProcessorArchitecture.AMD64.Equals(value, StringComparison.OrdinalIgnoreCase))
                {
                    _targetProcessorArchitecture = UtilitiesProcessorArchitecture.AMD64;
                }
                else if (UtilitiesProcessorArchitecture.IA64.Equals(value, StringComparison.OrdinalIgnoreCase))
                {
                    _targetProcessorArchitecture = UtilitiesProcessorArchitecture.IA64;
                }
                else if (UtilitiesProcessorArchitecture.ARM.Equals(value, StringComparison.OrdinalIgnoreCase))
                {
                    _targetProcessorArchitecture = UtilitiesProcessorArchitecture.ARM;
                }
                else
                {
                    _targetProcessorArchitecture = value;
                }
            }
        }

        private string _targetProcessorArchitecture;

        /// <summary>
        /// Property to allow multitargeting of ResolveComReferences:  If true, tlbimp.exe 
        /// from the appropriate target framework will be run out-of-proc to generate
        /// the necessary wrapper assemblies. Aximp is always run out of proc.
        /// </summary>
        public bool ExecuteAsTool { get; set; } = true;

        /// <summary>
        /// paths to found/generated reference wrappers
        /// </summary>
        [Output]
        public ITaskItem[] ResolvedFiles { get; set; }

        /// <summary>
        /// paths to found modules (needed for isolation)
        /// </summary>
        [Output]
        public ITaskItem[] ResolvedModules { get; set; }

        /// <summary>
        /// If ExecuteAsTool is true, this must be set to the SDK 
        /// tools path for the framework version being targeted. 
        /// </summary>
        public string SdkToolsPath { get; set; }

        /// <summary>
        /// Cache file for COM component timestamps. If not present, every run will regenerate all the wrappers.
        /// </summary>
        public string StateFile { get; set; }

        /// <summary>
        /// The project target framework version.
        ///
        /// Default is empty. which means there will be no filtering for the reference based on their target framework.
        /// </summary>
        /// <value></value>
        public string TargetFrameworkVersion { get; set; } = String.Empty;

        #endregion

        #region ITask members

        /// <summary>
        /// Task entry point.
        /// </summary>
        /// <returns></returns>
        public override bool Execute()
        {
            Log.LogErrorFromResources("TaskRequiresFrameworkFailure", nameof(ResolveComReference));
            return false;
        }

        #endregion
    }
}
