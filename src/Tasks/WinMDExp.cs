// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Export a windows metadata file from a dll</summary>
//-----------------------------------------------------------------------

using System;
using System.IO;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using System.Text;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Exports a managed assembly to a windows runtime metadata.
    /// </summary>
    public class WinMDExp : ToolTaskExtension
    {
        #region Properties

        /// <summary>
        /// Set of references to pass to the winmdexp tool.
        /// </summary>
        [Required]
        public ITaskItem[] References
        {
            get
            {
                return (ITaskItem[])Bag["References"];
            }

            set
            {
                ErrorUtilities.VerifyThrowArgumentNull(value, "References");
                Bag["References"] = value;
            }
        }

        /// <summary>
        /// Warning codes to disable
        /// </summary>
        public string DisabledWarnings
        {
            get
            {
                return (string)Bag["DisabledWarnings"];
            }

            set
            {
                ErrorUtilities.VerifyThrowArgumentNull(value, "DisabledWarnings");
                Bag["DisabledWarnings"] = value;
            }
        }

        /// <summary>
        /// Input documentation file
        /// </summary>
        public string InputDocumentationFile
        {
            get
            {
                return (string)Bag["InputDocumentationFile"];
            }

            set
            {
                ErrorUtilities.VerifyThrowArgumentNull(value, "InputDocumentationFile");
                Bag["InputDocumentationFile"] = value;
            }
        }

        /// <summary>
        /// Output documentation file
        /// </summary>
        public string OutputDocumentationFile
        {
            get
            {
                return (string)Bag["OutputDocumentationFile"];
            }

            set
            {
                ErrorUtilities.VerifyThrowArgumentNull(value, "OutputDocumentationFile");
                Bag["OutputDocumentationFile"] = value;
            }
        }

        /// <summary>
        /// Input PDB file
        /// </summary>
        public string InputPDBFile
        {
            get
            {
                return (string)Bag["InputPDBFile"];
            }

            set
            {
                ErrorUtilities.VerifyThrowArgumentNull(value, "InputPDBFile");
                Bag["InputPDBFile"] = value;
            }
        }

        /// <summary>
        /// Output PDB file
        /// </summary>
        public string OutputPDBFile
        {
            get
            {
                return (string)Bag["OutputPDBFile"];
            }

            set
            {
                ErrorUtilities.VerifyThrowArgumentNull(value, "OutputPDBFile");
                Bag["OutputPDBFile"] = value;
            }
        }

        /// <summary>
        /// WinMDModule to generate the WinMDFile for.
        /// </summary>
        [Required]
        public string WinMDModule
        {
            get
            {
                return (string)Bag["WinMDModule"];
            }

            set
            {
                ErrorUtilities.VerifyThrowArgumentNull(value, "WinMDModule");
                Bag["WinMDModule"] = value;
            }
        }

        /// <summary>
        /// Output windows metadata file  .winmd
        /// </summary>
        [Output]
        public string OutputWindowsMetadataFile
        {
            get
            {
                return (string)Bag["OutputWindowsMetadataFile"];
            }

            set
            {
                ErrorUtilities.VerifyThrowArgumentNull(value, "OutputWindowsMetadataFile");
                Bag["OutputWindowsMetadataFile"] = value;
            }
        }

        /// <summary>
        /// Path to the SDK directory which contains this tool
        /// </summary>
        public string SdkToolsPath
        {
            get { return (string)Bag["SdkToolsPath"]; }
            set { Bag["SdkToolsPath"] = value; }
        }

        /// <summary>
        /// Use output stream encoding as UTF-8.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "UTF", Justification = "Not worth breaking customers because of case correction")]
        public bool UTF8Output
        {
            get { return (bool)Bag["UTF8Output"]; }
            set { Bag["UTF8Output"] = value; }
        }

        /// <summary>
        /// Path to the SDK directory which contains this tool
        /// </summary>
        public bool TreatWarningsAsErrors
        {
            get { return (bool)Bag["TreatWarningsAsErrors"]; }
            set { Bag["TreatWarningsAsErrors"] = value; }
        }

        /// <summary>
        /// The policy used for assembly unification.
        /// </summary>
        public string AssemblyUnificationPolicy
        {
            get
            {
                return (string)Bag["AssemblyUnificationPolicy"];
            }

            set
            {
                ErrorUtilities.VerifyThrowArgumentNull(value, "AssemblyUnificationPolicy");
                Bag["AssemblyUnificationPolicy"] = value;
            }
        }

        /// <summary>
        /// The name of the tool to execute.
        /// </summary>
        override protected string ToolName
        {
            get
            {
                return "winmdexp.exe";
            }
        }

        /// <summary>
        /// Overridable property specifying the encoding of the captured task standard output stream
        /// </summary>
        protected override Encoding StandardOutputEncoding
        {
            get
            {
                return (UTF8Output) ? Encoding.UTF8 : base.StandardOutputEncoding;
            }
        }

        /// <summary>
        /// Overridable property specifying the encoding of the captured task standard error stream
        /// </summary>
        protected override Encoding StandardErrorEncoding
        {
            get
            {
                return (UTF8Output) ? Encoding.UTF8 : base.StandardErrorEncoding;
            }
        }

        #endregion

        #region Tool Members

        /// <summary>
        /// Fills the provided CommandLineBuilderExtension with all the command line options used when
        /// executing this tool
        /// </summary>
        /// <param name="commandLine">Gets filled with command line commands</param>
        protected internal override void AddCommandLineCommands(CommandLineBuilderExtension commandLine)
        {
            commandLine.AppendSwitchIfNotNull("/d:", OutputDocumentationFile);
            commandLine.AppendSwitchIfNotNull("/md:", InputDocumentationFile);
            commandLine.AppendSwitchIfNotNull("/mp:", InputPDBFile);
            commandLine.AppendSwitchIfNotNull("/pdb:", OutputPDBFile);
            commandLine.AppendSwitchIfNotNull("/assemblyunificationpolicy:", AssemblyUnificationPolicy);

            if (String.IsNullOrEmpty(OutputWindowsMetadataFile))
            {
                OutputWindowsMetadataFile = Path.ChangeExtension(WinMDModule, ".winmd");
            }

            commandLine.AppendSwitchIfNotNull("/out:", OutputWindowsMetadataFile);
            commandLine.AppendSwitchWithSplitting("/nowarn:", DisabledWarnings, ",", ';', ',');
            commandLine.AppendWhenTrue("/warnaserror+", this.Bag, "TreatWarningsAsErrors");
            commandLine.AppendWhenTrue("/utf8output", this.Bag, "UTF8Output");

            if (References != null)
            {
                // Loop through all the references passed in.  We'll be adding separate
                foreach (ITaskItem reference in this.References)
                {
                    commandLine.AppendSwitchIfNotNull("/reference:", reference.ItemSpec);
                }
            }

            commandLine.AppendFileNameIfNotNull(WinMDModule);

            base.AddCommandLineCommands(commandLine);
        }

        /// <summary>
        /// The full path of the tool to execute.
        /// </summary>
        override protected string GenerateFullPathToTool()
        {
            string pathToTool = null;

            if (String.IsNullOrEmpty(pathToTool) || !File.Exists(pathToTool))
            {
                pathToTool = SdkToolsPathUtility.GeneratePathToTool(SdkToolsPathUtility.FileInfoExists, Microsoft.Build.Utilities.ProcessorArchitecture.CurrentProcessArchitecture, SdkToolsPath, ToolExe, Log, true);
            }

            return pathToTool;
        }

        /// <summary>
        /// Validate parameters, log errors and warnings and return true if Execute should proceed.
        /// </summary>
        override protected bool ValidateParameters()
        {
            if (References == null)
            {
                Log.LogErrorWithCodeFromResources("WinMDExp.MustPassReferences");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Returns true if task execution is not necessary. Executed after ValidateParameters
        /// </summary>
        protected override bool SkipTaskExecution()
        {
            if (!String.IsNullOrEmpty(OutputWindowsMetadataFile))
            {
                var outputWriteTime = NativeMethodsShared.GetLastWriteFileUtcTime(OutputWindowsMetadataFile);
                var winMDModuleWriteTime = NativeMethodsShared.GetLastWriteFileUtcTime(WinMDModule);

                // If the last write time of the input file is less than the last write time of the output file 
                // then the output is newer then the input so we do not need to re-run the tool.
                if (outputWriteTime > winMDModuleWriteTime)
                {
                    return true;
                }
            }

            return false;
        }
        #endregion
    }
}
