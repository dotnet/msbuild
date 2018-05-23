// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Export a windows metadata file from a dll</summary>
//-----------------------------------------------------------------------

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

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
            get => (ITaskItem[])Bag[nameof(References)];

            set
            {
                ErrorUtilities.VerifyThrowArgumentNull(value, nameof(References));
                Bag[nameof(References)] = value;
            }
        }

        /// <summary>
        /// Warning codes to disable
        /// </summary>
        public string DisabledWarnings
        {
            get => (string)Bag[nameof(DisabledWarnings)];

            set
            {
                ErrorUtilities.VerifyThrowArgumentNull(value, nameof(DisabledWarnings));
                Bag[nameof(DisabledWarnings)] = value;
            }
        }

        /// <summary>
        /// Input documentation file
        /// </summary>
        public string InputDocumentationFile
        {
            get => (string)Bag[nameof(InputDocumentationFile)];

            set
            {
                ErrorUtilities.VerifyThrowArgumentNull(value, nameof(InputDocumentationFile));
                Bag[nameof(InputDocumentationFile)] = value;
            }
        }

        /// <summary>
        /// Output documentation file
        /// </summary>
        public string OutputDocumentationFile
        {
            get => (string)Bag[nameof(OutputDocumentationFile)];

            set
            {
                ErrorUtilities.VerifyThrowArgumentNull(value, nameof(OutputDocumentationFile));
                Bag[nameof(OutputDocumentationFile)] = value;
            }
        }

        /// <summary>
        /// Input PDB file
        /// </summary>
        public string InputPDBFile
        {
            get => (string)Bag[nameof(InputPDBFile)];

            set
            {
                ErrorUtilities.VerifyThrowArgumentNull(value, nameof(InputPDBFile));
                Bag[nameof(InputPDBFile)] = value;
            }
        }

        /// <summary>
        /// Output PDB file
        /// </summary>
        public string OutputPDBFile
        {
            get => (string)Bag[nameof(OutputPDBFile)];

            set
            {
                ErrorUtilities.VerifyThrowArgumentNull(value, nameof(OutputPDBFile));
                Bag[nameof(OutputPDBFile)] = value;
            }
        }

        /// <summary>
        /// WinMDModule to generate the WinMDFile for.
        /// </summary>
        [Required]
        public string WinMDModule
        {
            get => (string)Bag[nameof(WinMDModule)];

            set
            {
                ErrorUtilities.VerifyThrowArgumentNull(value, nameof(WinMDModule));
                Bag[nameof(WinMDModule)] = value;
            }
        }

        /// <summary>
        /// Output windows metadata file  .winmd
        /// </summary>
        [Output]
        public string OutputWindowsMetadataFile
        {
            get => (string)Bag[nameof(OutputWindowsMetadataFile)];

            set
            {
                ErrorUtilities.VerifyThrowArgumentNull(value, nameof(OutputWindowsMetadataFile));
                Bag[nameof(OutputWindowsMetadataFile)] = value;
            }
        }

        /// <summary>
        /// Path to the SDK directory which contains this tool
        /// </summary>
        public string SdkToolsPath
        {
            get => (string)Bag[nameof(SdkToolsPath)];
            set => Bag[nameof(SdkToolsPath)] = value;
        }

        /// <summary>
        /// Use output stream encoding as UTF-8.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "UTF", Justification = "Not worth breaking customers because of case correction")]
        public bool UTF8Output
        {
            get => (bool)Bag[nameof(UTF8Output)];
            set => Bag[nameof(UTF8Output)] = value;
        }

        /// <summary>
        /// Path to the SDK directory which contains this tool
        /// </summary>
        public bool TreatWarningsAsErrors
        {
            get => (bool)Bag[nameof(TreatWarningsAsErrors)];
            set => Bag[nameof(TreatWarningsAsErrors)] = value;
        }

        /// <summary>
        /// The policy used for assembly unification.
        /// </summary>
        public string AssemblyUnificationPolicy
        {
            get => (string)Bag[nameof(AssemblyUnificationPolicy)];

            set
            {
                ErrorUtilities.VerifyThrowArgumentNull(value, nameof(AssemblyUnificationPolicy));
                Bag[nameof(AssemblyUnificationPolicy)] = value;
            }
        }

        /// <summary>
        /// The name of the tool to execute.
        /// </summary>
        protected override string ToolName => "winmdexp.exe";

        /// <summary>
        /// Overridable property specifying the encoding of the captured task standard output stream
        /// </summary>
        protected override Encoding StandardOutputEncoding => UTF8Output ? Encoding.UTF8 : base.StandardOutputEncoding;

        /// <summary>
        /// Overridable property specifying the encoding of the captured task standard error stream
        /// </summary>
        protected override Encoding StandardErrorEncoding => UTF8Output ? Encoding.UTF8 : base.StandardErrorEncoding;

        protected override bool UseNewLineSeparatorInResponseFile => true;

        #endregion

        #region Tool Members

        protected internal override void AddResponseFileCommands(CommandLineBuilderExtension commandLine)
        {
            commandLine.AppendSwitchUnquotedIfNotNull("/d:", OutputDocumentationFile);
            commandLine.AppendSwitchUnquotedIfNotNull("/md:", InputDocumentationFile);
            commandLine.AppendSwitchUnquotedIfNotNull("/mp:", InputPDBFile);
            commandLine.AppendSwitchUnquotedIfNotNull("/pdb:", OutputPDBFile);
            commandLine.AppendSwitchUnquotedIfNotNull("/assemblyunificationpolicy:", AssemblyUnificationPolicy);

            if (String.IsNullOrEmpty(OutputWindowsMetadataFile))
            {
                OutputWindowsMetadataFile = Path.ChangeExtension(WinMDModule, ".winmd");
            }

            commandLine.AppendSwitchUnquotedIfNotNull("/out:", OutputWindowsMetadataFile);
            commandLine.AppendSwitchWithSplitting("/nowarn:", DisabledWarnings, ",", ';', ',');
            commandLine.AppendWhenTrue("/warnaserror+", Bag, "TreatWarningsAsErrors");
            commandLine.AppendWhenTrue("/utf8output", Bag, "UTF8Output");

            if (References != null)
            {
                // Loop through all the references passed in.  We'll be adding separate
                foreach (ITaskItem reference in References)
                {
                    commandLine.AppendSwitchUnquotedIfNotNull("/reference:", reference.ItemSpec);
                }
            }

            // There is no public method to add unquoted text that includes a separator.  Calling this method with String.Empty adds a separator
            // and the unquoted text and no parameter.
            commandLine.AppendSwitchUnquotedIfNotNull(WinMDModule, String.Empty);
            base.AddResponseFileCommands(commandLine);
        }

        /// <summary>
        /// The full path of the tool to execute.
        /// </summary>
        protected override string GenerateFullPathToTool()
        {
            return SdkToolsPathUtility.GeneratePathToTool(SdkToolsPathUtility.FileInfoExists, Microsoft.Build.Utilities.ProcessorArchitecture.CurrentProcessArchitecture, SdkToolsPath, ToolExe, Log, true);
        }

        /// <summary>
        /// Validate parameters, log errors and warnings and return true if Execute should proceed.
        /// </summary>
        protected override bool ValidateParameters()
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
