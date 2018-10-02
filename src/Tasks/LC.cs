// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Build.Tasks.Deployment.ManifestUtilities;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// The License Compiler task
    /// </summary>
    public class LC : ToolTaskExtension
    {
        #region Input/output properties

        /// <summary>
        /// Specifies the items that contain licensed components that need to be included in the .licenses file
        /// </summary>
        [Required]
        public ITaskItem[] Sources
        {
            set => Bag[nameof(Sources)] = value;
            get => (ITaskItem[])Bag[nameof(Sources)];
        }

        /// <summary>
        /// The name of the .licenses file, output only. It's inferred from LicenseTarget and OutputDirectory.
        /// </summary>
        [Output]
        public ITaskItem OutputLicense
        {
            set => Bag[nameof(OutputLicense)] = value;
            get => (ITaskItem)Bag[nameof(OutputLicense)];
        }

        /// <summary>
        /// Specifies the executable for which the .licenses files are being generated
        /// </summary>
        [Required]
        public ITaskItem LicenseTarget
        {
            set => Bag[nameof(LicenseTarget)] = value;
            get => (ITaskItem)Bag[nameof(LicenseTarget)];
        }

        /// <summary>
        /// Output directory for the generated .licenses file
        /// </summary>
        /// <value></value>
        public string OutputDirectory
        {
            set => Bag[nameof(OutputDirectory)] = value;
            get => (string)Bag[nameof(OutputDirectory)];
        }

        /// <summary>
        /// Specifies the referenced components (licensed controls and possibly their dependent assemblies)
        /// to load when generating the .license file.
        /// </summary>
        public ITaskItem[] ReferencedAssemblies
        {
            set => Bag[nameof(ReferencedAssemblies)] = value;
            get => (ITaskItem[])Bag[nameof(ReferencedAssemblies)];
        }

        /// <summary>
        /// Suppresses the display of the startup banner
        /// </summary>
        public bool NoLogo
        {
            set => Bag[nameof(NoLogo)] = value;
            get => GetBoolParameterWithDefault(nameof(NoLogo), false);
        }

        public string SdkToolsPath
        {
            set => Bag[nameof(SdkToolsPath)] = value;
            get => (string)Bag[nameof(SdkToolsPath)];
        }

        /// <summary>
        /// Targeted version of the framework (i.e. 4.5 or 2.0, etc.)
        /// </summary>
        [Required]
        public string TargetFrameworkVersion { get; set; }
        #endregion

        #region Class properties

        /// <summary>
        /// The name of the tool to execute
        /// </summary>
        protected override string ToolName => "lc.exe";

        #endregion

        #region Methods

        /// <summary>
        /// Validate the task arguments, log any warnings/errors
        /// </summary>
        /// <returns>true if arguments are corrent enough to continue processing, false otherwise</returns>
        protected override bool ValidateParameters()
        {
            // if all the Required attributes are set, we're good to go.
            return true;
        }

        /// <summary>
        /// Determing the path to lc.exe
        /// </summary>
        /// <returns>path to lc.exe, null if not found</returns>
        protected override string GenerateFullPathToTool()
        {
            string pathToTool = SdkToolsPathUtility.GeneratePathToTool(SdkToolsPathUtility.FileInfoExists, ProcessorArchitecture.CurrentProcessArchitecture, SdkToolsPath, ToolExe, Log, true);
            return pathToTool;
        }

        /// <summary>
        /// Generates arguments to be passed to lc.exe
        /// </summary>
        /// <param name="commandLine">command line builder class to add arguments to</param>
        private void AddCommands(CommandLineBuilderExtension commandLine)
        {
            commandLine.AppendSwitchIfNotNull("/target:", LicenseTarget.ItemSpec);

            foreach (ITaskItem item in Sources)
            {
                commandLine.AppendSwitchIfNotNull("/complist:", item.ItemSpec);
            }

            commandLine.AppendSwitchIfNotNull("/outdir:", OutputDirectory);

            if (ReferencedAssemblies != null)
            {
                foreach (ITaskItem item in ReferencedAssemblies)
                {
                    commandLine.AppendSwitchIfNotNull("/i:", item.ItemSpec);
                }
            }

            commandLine.AppendWhenTrue("/nologo", Bag, "NoLogo");

            // generate the output file name
            string outputPath = LicenseTarget.ItemSpec + ".licenses";

            if (OutputDirectory != null)
            {
                outputPath = Path.Combine(OutputDirectory, outputPath);
            }

            OutputLicense = new TaskItem(outputPath);
        }

        /// <summary>
        /// Generates response file with arguments for lc.exe
        /// Used when targeting framework version is 4.6 or later
        /// </summary>
        /// <param name="commandLine">command line builder class to add arguments to the response file</param>
        protected internal override void AddResponseFileCommands(CommandLineBuilderExtension commandLine)
        {
            Version targetFramework = Util.GetTargetFrameworkVersion(TargetFrameworkVersion);
            // Don't generate response file on versions of the framework < 4.6
            // They will use an older lc.exe which does not understand response files
            if (targetFramework.CompareTo(new Version("4.6")) < 0)
            {
                return;
            }

            AddCommands(commandLine);
        }

        /// <summary>
        /// Generates command line arguments for lc.exe
        /// Used when targeting framework version is less than 4.6
        /// </summary>
        /// <param name="commandLine">command line builder class to add arguments to the command line</param>
        protected internal override void AddCommandLineCommands(CommandLineBuilderExtension commandLine)
        {
            Version targetFramework = Util.GetTargetFrameworkVersion(TargetFrameworkVersion);
            // If the target framework version is < 4.6, we will be using lc.exe from an older SDK
            // In this case, we want to use command line parameters instead of a response file
            if (targetFramework.CompareTo(new Version("4.6")) >= 0)
            {
                return;
            }

            AddCommands(commandLine);
        }

        #endregion
    }
}
