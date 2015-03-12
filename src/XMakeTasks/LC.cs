// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// The License Compiler task
    /// </summary>
    public class LC : ToolTaskExtension
    {
        #region Constructors

        /// <summary>
        /// public constructor
        /// </summary>
        public LC()
        {
            // do nothing
        }

        #endregion

        #region Input/output properties

        /// <summary>
        /// Specifies the items that contain licensed components that need to be included in the .licenses file
        /// </summary>
        [Required]
        public ITaskItem[] Sources
        {
            set { Bag["Sources"] = value; }
            get { return (ITaskItem[])Bag["Sources"]; }
        }

        /// <summary>
        /// The name of the .licenses file, output only. It's inferred from LicenseTarget and OutputDirectory.
        /// </summary>
        [Output]
        public ITaskItem OutputLicense
        {
            set { Bag["OutputLicense"] = value; }
            get { return (ITaskItem)Bag["OutputLicense"]; }
        }

        /// <summary>
        /// Specifies the executable for which the .licenses files are being generated
        /// </summary>
        [Required]
        public ITaskItem LicenseTarget
        {
            set { Bag["LicenseTarget"] = value; }
            get { return (ITaskItem)Bag["LicenseTarget"]; }
        }

        /// <summary>
        /// Output directory for the generated .licenses file
        /// </summary>
        /// <value></value>
        public string OutputDirectory
        {
            set { Bag["OutputDirectory"] = value; }
            get { return (string)Bag["OutputDirectory"]; }
        }

        /// <summary>
        /// Specifies the referenced components (licensed controls and possibly their dependent assemblies)
        /// to load when generating the .license file.
        /// </summary>
        public ITaskItem[] ReferencedAssemblies
        {
            set { Bag["ReferencedAssemblies"] = value; }
            get { return (ITaskItem[])Bag["ReferencedAssemblies"]; }
        }

        /// <summary>
        /// Suppresses the display of the startup banner
        /// </summary>
        public bool NoLogo
        {
            set { Bag["NoLogo"] = value; }
            get { return GetBoolParameterWithDefault("NoLogo", false); }
        }

        public string SdkToolsPath
        {
            set { Bag["SdkToolsPath"] = value; }
            get { return (string)Bag["SdkToolsPath"]; }
        }
        #endregion

        #region Class properties

        /// <summary>
        /// The name of the tool to execute
        /// </summary>
        protected override string ToolName
        {
            get
            {
                return "LC.exe";
            }
        }

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
            string pathToTool = SdkToolsPathUtility.GeneratePathToTool(SdkToolsPathUtility.FileInfoExists, Microsoft.Build.Utilities.ProcessorArchitecture.CurrentProcessArchitecture, SdkToolsPath, ToolName, Log, true);
            return pathToTool;
        }

        /// <summary>
        /// Generates response file with arguments for lc.exe
        /// </summary>
        /// <param name="commandLine">command line builder class to add arguments to the response file</param>
        protected internal override void AddResponseFileCommands(CommandLineBuilderExtension commandLine)
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
                outputPath = Path.Combine(OutputDirectory, outputPath);

            OutputLicense = new TaskItem(outputPath);
        }

        #endregion
    }
}
