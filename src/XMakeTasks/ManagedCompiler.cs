// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// This class defines all of the common stuff that is shared between Vjc, Vbc and Csc tasks.
    /// This class is not instantiatable as a Task just by itself.
    ///
    /// The security attribute below is there to make sure that inheriting classes are MS only
    /// (FxCop suggestion since we're using virtual internal methods)
    /// </summary>
    [System.Security.Permissions.StrongNameIdentityPermission(System.Security.Permissions.SecurityAction.InheritanceDemand,
        PublicKey =
            "00240000048000009400000006020000" +
            "00240000525341310004000001000100" +
            "07d1fa57c4aed9f0a32e84aa0faefd0d" +
            "e9e8fd6aec8f87fb03766c834c99921e" +
            "b23be79ad9d5dcc1dd9ad23613210290" +
            "0b723cf980957fc4e177108fc607774f" +
            "29e8320e92ea05ece4e821c0a5efe8f1" +
            "645c4c0c93c1ab99285d622caa652c1d" +
            "fad63d745d6f2de5f17e5eaf0fc4963d" +
            "261c8a12436518206dc093344d5ad293"
        )]
    public abstract class ManagedCompiler : ToolTaskExtension
    {
        #region Properties

        // Please keep these alphabetized.
        public string[] AdditionalLibPaths
        {
            set { Bag["AdditionalLibPaths"] = value; }
            get { return (string[])Bag["AdditionalLibPaths"]; }
        }

        public string[] AddModules
        {
            set { Bag["AddModules"] = value; }
            get { return (string[])Bag["AddModules"]; }
        }

        public ITaskItem[] AdditionalFiles
        {
            set { Bag["AdditionalFiles"] = value; }
            get { return (ITaskItem[])Bag["AdditionalFiles"]; }
        }

        public ITaskItem[] Analyzers
        {
            set { Bag["Analyzers"] = value; }
            get { return (ITaskItem[])Bag["Analyzers"]; }
        }

        // We do not support BugReport because it always requires user interaction,
        // which will cause a hang.

        public string CodeAnalysisRuleSet
        {
            set { Bag["CodeAnalysisRuleSet"] = value; }
            get { return (string)Bag["CodeAnalysisRuleSet"]; }
        }

        public int CodePage
        {
            set { Bag["CodePage"] = value; }
            get { return GetIntParameterWithDefault("CodePage", 0); }
        }

        public string DebugType
        {
            set { Bag["DebugType"] = value; }
            get { return (string)Bag["DebugType"]; }
        }

        public string DefineConstants
        {
            set { Bag["DefineConstants"] = value; }
            get { return (string)Bag["DefineConstants"]; }
        }

        public bool DelaySign
        {
            set { Bag["DelaySign"] = value; }
            get { return GetBoolParameterWithDefault("DelaySign", false); }
        }

        public bool EmitDebugInformation
        {
            set { Bag["EmitDebugInformation"] = value; }
            get { return GetBoolParameterWithDefault("EmitDebugInformation", false); }
        }

        public int FileAlignment
        {
            set { Bag["FileAlignment"] = value; }
            get { return GetIntParameterWithDefault("FileAlignment", 0); }
        }

        public bool HighEntropyVA
        {
            set { Bag["HighEntropyVA"] = value; }
            get { return GetBoolParameterWithDefault("HighEntropyVA", false); }
        }

        public string KeyContainer
        {
            set { Bag["KeyContainer"] = value; }
            get { return (string)Bag["KeyContainer"]; }
        }

        public string KeyFile
        {
            set { Bag["KeyFile"] = value; }
            get { return (string)Bag["KeyFile"]; }
        }

        public ITaskItem[] LinkResources
        {
            set { Bag["LinkResources"] = value; }
            get { return (ITaskItem[])Bag["LinkResources"]; }
        }

        public string MainEntryPoint
        {
            set { Bag["MainEntryPoint"] = value; }
            get { return (string)Bag["MainEntryPoint"]; }
        }

        public bool NoConfig
        {
            set { Bag["NoConfig"] = value; }
            get { return GetBoolParameterWithDefault("NoConfig", false); }
        }

        public bool NoLogo
        {
            set { Bag["NoLogo"] = value; }
            get { return GetBoolParameterWithDefault("NoLogo", false); }
        }

        public bool NoWin32Manifest
        {
            set { Bag["NoWin32Manifest"] = value; }
            get { return GetBoolParameterWithDefault("NoWin32Manifest", false); }
        }

        public bool Optimize
        {
            set { Bag["Optimize"] = value; }
            get { return GetBoolParameterWithDefault("Optimize", false); }
        }

        [Output]
        public ITaskItem OutputAssembly
        {
            set { Bag["OutputAssembly"] = value; }
            get { return (ITaskItem)Bag["OutputAssembly"]; }
        }

        public string Platform
        {
            set { Bag["Platform"] = value; }
            get { return (string)Bag["Platform"]; }
        }

        public bool Prefer32Bit
        {
            set { Bag["Prefer32Bit"] = value; }
            get { return GetBoolParameterWithDefault("Prefer32Bit", false); }
        }

        public ITaskItem[] References
        {
            set { Bag["References"] = value; }
            get { return (ITaskItem[])Bag["References"]; }
        }

        public ITaskItem[] Resources
        {
            set { Bag["Resources"] = value; }
            get { return (ITaskItem[])Bag["Resources"]; }
        }

        public ITaskItem[] ResponseFiles
        {
            set { Bag["ResponseFiles"] = value; }
            get { return (ITaskItem[])Bag["ResponseFiles"]; }
        }



        public ITaskItem[] Sources
        {
            set
            {
                if (UsedCommandLineTool)
                {
                    NormalizePaths(value);
                }

                Bag["Sources"] = value;
            }
            get { return (ITaskItem[])Bag["Sources"]; }
        }

        public string SubsystemVersion
        {
            set { Bag["SubsystemVersion"] = value; }
            get { return (string)Bag["SubsystemVersion"]; }
        }

        public string TargetType
        {
            set { Bag["TargetType"] = value.ToLower(CultureInfo.InvariantCulture); }
            get { return (string)Bag["TargetType"]; }
        }

        public bool TreatWarningsAsErrors
        {
            set { Bag["TreatWarningsAsErrors"] = value; }
            get { return GetBoolParameterWithDefault("TreatWarningsAsErrors", false); }
        }

        public bool Utf8Output
        {
            set { Bag["Utf8Output"] = value; }
            get { return GetBoolParameterWithDefault("Utf8Output", false); }
        }

        public string Win32Icon
        {
            set { Bag["Win32Icon"] = value; }
            get { return (string)Bag["Win32Icon"]; }
        }

        public string Win32Manifest
        {
            set { Bag["Win32Manifest"] = value; }
            get { return (string)Bag["Win32Manifest"]; }
        }

        public string Win32Resource
        {
            set { Bag["Win32Resource"] = value; }
            get { return (string)Bag["Win32Resource"]; }
        }

        // Map explicit platform of "AnyCPU" or the default platform (null or ""), since it is commonly understood in the 
        // managed build process to be equivalent to "AnyCPU", to platform "AnyCPU32BitPreferred" if the Prefer32Bit 
        // property is set. 
        internal string PlatformWith32BitPreference
        {
            get
            {
                string platform = this.Platform;
                if ((String.IsNullOrEmpty(platform) || platform.Equals("anycpu", StringComparison.OrdinalIgnoreCase)) && this.Prefer32Bit)
                {
                    platform = "anycpu32bitpreferred";
                }
                return platform;
            }
        }

        /// <summary>
        /// Overridable property specifying the encoding of the captured task standard output stream
        /// </summary>
        protected override Encoding StandardOutputEncoding
        {
            get
            {
                return (Utf8Output) ? Encoding.UTF8 : base.StandardOutputEncoding;
            }
        }

        #endregion

        /// <summary>
        /// If an alternate tool name or tool path was specified in the project file, we don't want to
        /// use the host compiler for IDE builds.
        /// </summary>
        /// <returns>false if the host compiler should be used</returns>
        protected internal virtual bool UseAlternateCommandLineToolToExecute()
        {
            // Roslyn MSBuild task does not support using host object for compilation
            return true;
        }

        #region Tool Members

        /// <summary>
        /// Fills the provided CommandLineBuilderExtension with those switches and other information that can go into a response file.
        /// </summary>
        protected internal override void AddResponseFileCommands(CommandLineBuilderExtension commandLine)
        {
            // If outputAssembly is not specified, then an "/out: <name>" option won't be added to
            // overwrite the one resulting from the OutputAssembly member of the CompilerParameters class.
            // In that case, we should set the outputAssembly member based on the first source file.
            if (
                    (OutputAssembly == null) &&
                    (Sources != null) &&
                    (Sources.Length > 0) &&
                    (this.ResponseFiles == null)    // The response file may already have a /out: switch in it, so don't try to be smart here.
                )
            {
                try
                {
                    OutputAssembly = new TaskItem(Path.GetFileNameWithoutExtension(Sources[0].ItemSpec));
                }
                catch (ArgumentException e)
                {
                    throw new ArgumentException(e.Message, "Sources");
                }
                if (String.Compare(TargetType, "library", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    OutputAssembly.ItemSpec += ".dll";
                }
                else if (String.Compare(TargetType, "module", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    OutputAssembly.ItemSpec += ".netmodule";
                }
                else
                {
                    OutputAssembly.ItemSpec += ".exe";
                }
            }

            commandLine.AppendSwitchIfNotNull("/addmodule:", this.AddModules, ",");
            commandLine.AppendSwitchWithInteger("/codepage:", this.Bag, "CodePage");

            ConfigureDebugProperties();

            // The "DebugType" parameter should be processed after the "EmitDebugInformation" parameter
            // because it's more specific.  Order matters on the command-line, and the last one wins.
            // /debug+ is just a shorthand for /debug:full.  And /debug- is just a shorthand for /debug:none.

            commandLine.AppendPlusOrMinusSwitch("/debug", this.Bag, "EmitDebugInformation");
            commandLine.AppendSwitchIfNotNull("/debug:", this.DebugType);

            commandLine.AppendPlusOrMinusSwitch("/delaysign", this.Bag, "DelaySign");

            commandLine.AppendSwitchWithInteger("/filealign:", this.Bag, "FileAlignment");
            commandLine.AppendSwitchIfNotNull("/keycontainer:", this.KeyContainer);
            commandLine.AppendSwitchIfNotNull("/keyfile:", this.KeyFile);
            // If the strings "LogicalName" or "Access" ever change, make sure to search/replace everywhere in vsproject.
            commandLine.AppendSwitchIfNotNull("/linkresource:", this.LinkResources, new string[] { "LogicalName", "Access" });
            commandLine.AppendWhenTrue("/nologo", this.Bag, "NoLogo");
            commandLine.AppendWhenTrue("/nowin32manifest", this.Bag, "NoWin32Manifest");
            commandLine.AppendPlusOrMinusSwitch("/optimize", this.Bag, "Optimize");
            commandLine.AppendSwitchIfNotNull("/out:", this.OutputAssembly);
            commandLine.AppendSwitchIfNotNull("/ruleset:", this.CodeAnalysisRuleSet);
            commandLine.AppendSwitchIfNotNull("/subsystemversion:", this.SubsystemVersion);
            // If the strings "LogicalName" or "Access" ever change, make sure to search/replace everywhere in vsproject.
            commandLine.AppendSwitchIfNotNull("/resource:", this.Resources, new string[] { "LogicalName", "Access" });
            commandLine.AppendSwitchIfNotNull("/target:", this.TargetType);
            commandLine.AppendPlusOrMinusSwitch("/warnaserror", this.Bag, "TreatWarningsAsErrors");
            commandLine.AppendWhenTrue("/utf8output", this.Bag, "Utf8Output");
            commandLine.AppendSwitchIfNotNull("/win32icon:", this.Win32Icon);
            commandLine.AppendSwitchIfNotNull("/win32manifest:", this.Win32Manifest);

            // Append the analyzers.
            this.AddAnalyzersToCommandLine(commandLine);

            // Append additional files.
            this.AddAdditionalFilesToCommandLine(commandLine);

            // Append the sources.
            commandLine.AppendFileNamesIfNotNull(Sources, " ");
        }

        /// <summary>
        /// Adds a "/analyzer:" switch to the command line for each provided analyzer.
        /// </summary>
        private void AddAnalyzersToCommandLine(CommandLineBuilderExtension commandLine)
        {
            // If there were no analyzers passed in, don't add any /analyzer: switches
            // on the command-line.
            if ((this.Analyzers == null) || (this.Analyzers.Length == 0))
            {
                return;
            }

            foreach (ITaskItem analyzer in this.Analyzers)
            {
                commandLine.AppendSwitchIfNotNull("/analyzer:", analyzer.ItemSpec);
            }
        }

        /// <summary>
        /// Adds a "/analyzer:" switch to the command line for each provided analyzer.
        /// </summary>
        private void AddAdditionalFilesToCommandLine(CommandLineBuilderExtension commandLine)
        {
            // If there were no additional files passed in, don't add any /additionalfile: switches
            // on the command-line.
            if ((this.AdditionalFiles == null) || (this.AdditionalFiles.Length == 0))
            {
                return;
            }

            foreach (ITaskItem additionalFile in this.AdditionalFiles)
            {
                commandLine.AppendSwitchIfNotNull("/additionalfile:", additionalFile.ItemSpec);
            }
        }

        /// <summary>
        /// Configure the debug switches which will be placed on the compiler commandline.
        /// The matrix of debug type and symbol inputs and the desired results is as follows:
        /// 
        /// Debug Symbols              DebugType   Desired Resilts
        ///          True               Full        /debug+ /debug:full
        ///          True               PdbOnly     /debug+ /debug:PdbOnly
        ///          True               None        /debug-
        ///          True               Blank       /debug+
        ///          False              Full        /debug- /debug:full
        ///          False              PdbOnly     /debug- /debug:PdbOnly
        ///          False              None        /debug-
        ///          False              Blank       /debug-
        ///          Blank              Full                /debug:full
        ///          Blank              PdbOnly             /debug:PdbOnly
        ///          Blank              None        /debug-
        /// Debug:   Blank              Blank       /debug+ //Microsof.common.targets will set this
        /// Release: Blank              Blank       "Nothing for either switch"
        /// 
        /// The logic is as follows:
        /// If debugtype is none  set debugtype to empty and debugSymbols to false
        /// If debugType is blank  use the debugsymbols "as is"
        /// If debug type is set, use its value and the debugsymbols value "as is"
        /// </summary>
        private void ConfigureDebugProperties()
        {
            // If debug type is set we need to take some action depending on the value. If debugtype is not set
            // We don't need to modify the EmitDebugInformation switch as its value will be used as is.
            if (Bag["DebugType"] != null)
            {
                // If debugtype is none then only show debug- else use the debug type and the debugsymbols as is.
                if (string.Compare((string)Bag["DebugType"], "none", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    Bag["DebugType"] = null;
                    Bag["EmitDebugInformation"] = false;
                }
            }
        }

        /// <summary>
        /// Fills the provided CommandLineBuilderExtension with those switches and other information that can't go into a response file and
        /// must go directly onto the command line.
        /// </summary>
        protected internal override void AddCommandLineCommands(CommandLineBuilderExtension commandLine)
        {
            commandLine.AppendWhenTrue("/noconfig", this.Bag, "NoConfig");
        }

        /// <summary>
        /// Validate parameters, log errors and warnings and return true if
        /// Execute should proceed.
        /// </summary>
        protected override bool ValidateParameters()
        {
            return ListHasNoDuplicateItems(this.Resources, "Resources", "LogicalName") && ListHasNoDuplicateItems(this.Sources, "Sources");
        }

        /// <summary>
        /// Returns true if the provided item list contains duplicate items, false otherwise.
        /// </summary>
        protected bool ListHasNoDuplicateItems(ITaskItem[] itemList, string parameterName)
        {
            return ListHasNoDuplicateItems(itemList, parameterName, null);
        }

        /// <summary>
        /// Returns true if the provided item list contains duplicate items, false otherwise.
        /// </summary>
        /// <param name="itemList"></param>
        /// <param name="disambiguatingMetadataName">Optional name of metadata that may legitimately disambiguate items. May be null.</param>
        /// <param name="parameterName"></param>
        private bool ListHasNoDuplicateItems(ITaskItem[] itemList, string parameterName, string disambiguatingMetadataName)
        {
            if (itemList == null || itemList.Length == 0)
            {
                return true;
            }

            Hashtable alreadySeen = new Hashtable(StringComparer.OrdinalIgnoreCase);
            foreach (ITaskItem item in itemList)
            {
                string key;
                string disambiguatingMetadataValue = null;
                if (disambiguatingMetadataName != null)
                {
                    disambiguatingMetadataValue = item.GetMetadata(disambiguatingMetadataName);
                }

                if (disambiguatingMetadataName == null || String.IsNullOrEmpty(disambiguatingMetadataValue))
                {
                    key = item.ItemSpec;
                }
                else
                {
                    key = item.ItemSpec + ":" + disambiguatingMetadataValue;
                }

                if (alreadySeen.ContainsKey(key))
                {
                    if (disambiguatingMetadataName == null || String.IsNullOrEmpty(disambiguatingMetadataValue))
                    {
                        Log.LogErrorWithCodeFromResources("General.DuplicateItemsNotSupported", item.ItemSpec, parameterName);
                    }
                    else
                    {
                        Log.LogErrorWithCodeFromResources("General.DuplicateItemsNotSupportedWithMetadata", item.ItemSpec, parameterName, disambiguatingMetadataValue, disambiguatingMetadataName);
                    }
                    return false;
                }
                else
                {
                    alreadySeen[key] = String.Empty;
                }
            }

            return true;
        }

        /// <summary>
        /// Allows tool to handle the return code.
        /// This method will only be called with non-zero exitCode.
        /// </summary>
        protected override bool HandleTaskExecutionErrors()
        {
            // For managed compilers, the compiler should emit the appropriate
            // error messages before returning a non-zero exit code, so we don't 
            // normally need to emit any additional messages now.
            //
            // If somehow the compiler DID return a non-zero exit code and didn't log an error, we'd like to log that exit code.
            // We can only do this for the command line compiler: if the inproc compiler was used, 
            // we can't tell what if anything it logged as it logs directly to Visual Studio's output window.
            //
            if (!Log.HasLoggedErrors && UsedCommandLineTool)
            {
                // This will log a message "MSB3093: The command exited with code {0}."
                base.HandleTaskExecutionErrors();
            }

            return false;
        }

        /// <summary>
        /// Takes a list of files and returns the normalized locations of these files
        /// </summary>
        private void NormalizePaths(ITaskItem[] taskItems)
        {
            foreach (var item in taskItems)
            {
                item.ItemSpec = FileUtilities.GetFullPathNoThrow(item.ItemSpec);
            }
        }

        /// <summary>
        /// Whether the command line compiler was invoked, instead
        /// of the host object compiler.
        /// </summary>
        protected bool UsedCommandLineTool
        {
            get;
            set;
        }

        private bool _hostCompilerSupportsAllParameters;
        protected bool HostCompilerSupportsAllParameters
        {
            get { return _hostCompilerSupportsAllParameters; }
            set { _hostCompilerSupportsAllParameters = value; }
        }

        /// <summary>
        /// Checks the bool result from calling one of the methods on the host compiler object to
        /// set one of the parameters.  If it returned false, that means the host object doesn't
        /// support a particular parameter or variation on a parameter.  So we log a comment,
        /// and set our state so we know not to call the host object to do the actual compilation.
        /// </summary>
        protected void CheckHostObjectSupport
            (
            string parameterName,
            bool resultFromHostObjectSetOperation
            )
        {
            if (!resultFromHostObjectSetOperation)
            {
                Log.LogMessageFromResources(MessageImportance.Normal, "General.ParameterUnsupportedOnHostCompiler", parameterName);
                _hostCompilerSupportsAllParameters = false;
            }
        }

        /// <summary>
        /// Checks to see whether all of the passed-in references exist on disk before we launch the compiler.
        /// </summary>
        protected bool CheckAllReferencesExistOnDisk()
        {
            if (null == this.References)
            {
                // No references
                return true;
            }

            bool success = true;

            foreach (ITaskItem reference in this.References)
            {
                if (!File.Exists(reference.ItemSpec))
                {
                    success = false;
                    Log.LogErrorWithCodeFromResources("General.ReferenceDoesNotExist", reference.ItemSpec);
                }
            }

            return success;
        }

        /// <summary>
        /// The IDE and command line compilers unfortunately differ in how win32 
        /// manifests are specified.  In particular, the command line compiler offers a 
        /// "/nowin32manifest" switch, while the IDE compiler does not offer analagous 
        /// functionality. If this switch is omitted from the command line and no win32 
        /// manifest is specified, the compiler will include a default win32 manifest 
        /// named "default.win32manifest" found in the same directory as the compiler 
        /// executable. Again, the IDE compiler does not offer analagous support.
        /// 
        /// We'd like to imitate the command line compiler's behavior in the IDE, but 
        /// it isn't aware of the default file, so we must compute the path to it if 
        /// noDefaultWin32Manifest is false and no win32Manifest was provided by the
        /// project.
        ///
        /// This method will only be called during the initialization of the host object,
        /// which is only used during IDE builds.
        /// </summary>
        /// <param name="noDefaultWin32Manifest"></param>
        /// <param name="win32Manifest"></param>
        /// <returns>the path to the win32 manifest to provide to the host object</returns>
        internal string GetWin32ManifestSwitch
        (
            bool noDefaultWin32Manifest,
            string win32Manifest
        )
        {
            if (!noDefaultWin32Manifest)
            {
                if (String.IsNullOrEmpty(win32Manifest) && String.IsNullOrEmpty(this.Win32Resource))
                {
                    // We only want to consider the default.win32manifest if this is an executable
                    if (!String.Equals(TargetType, "library", StringComparison.OrdinalIgnoreCase)
                       && !String.Equals(TargetType, "module", StringComparison.OrdinalIgnoreCase))
                    {
                        // We need to compute the path to the default win32 manifest
                        string pathToDefaultManifest = ToolLocationHelper.GetPathToDotNetFrameworkFile
                                                       (
                                                           "default.win32manifest",
                                                           TargetDotNetFrameworkVersion.VersionLatest
                                                       );

                        if (null == pathToDefaultManifest)
                        {
                            // This is rather unlikely, and the inproc compiler seems to log an error anyway.
                            // So just a message is fine.
                            Log.LogMessageFromResources
                            (
                                "General.ExpectedFileMissing",
                                "default.win32manifest"
                            );
                        }

                        return pathToDefaultManifest;
                    }
                }
            }

            return win32Manifest;
        }

        #endregion
    }
}
