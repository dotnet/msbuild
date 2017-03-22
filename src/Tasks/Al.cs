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
    /// This class defines the "AL" XMake task, which enables using al.exe to link
    /// modules and resource files into assemblies.
    /// </summary>
    public class AL : ToolTaskExtension
    {
        #region Properties
        /*
        Microsoft (R) Assembly Linker version 7.10.2175
        for Microsoft (R) .NET Framework version 1.2
        Copyright (C) Microsoft Corporation 2001-2015. All rights reserved.
        
        Usage: al [options] [sources]
        Options: ('/out' must be specified)
        
          /? or /help               Display this usage message
          @<filename>               Read response file for more options
          /algid:<id>               Algorithm used to hash files (in hexadecimal)
          /base[address]:<addr>     Base address for the library
          /bugreport:<filename>     Create a 'Bug Report' file
          /comp[any]:<text>         Company name
          /config[uration]:<text>   Configuration string
          /copy[right]:<text>       Copyright message
          /c[ulture]:<text>         Supported culture
          /delay[sign][+|-]         Delay sign this assembly
          /descr[iption]:<text>     Description
          /e[vidence]:<filename>    Security evidence file to embed
          /fileversion:<version>    Optional Win32 version (overrides assembly version)
          /flags:<flags>            Assembly flags  (in hexadecimal)
          /fullpaths                Display files using fully-qualified filenames
          /keyf[ile]:<filename>     File containing key to sign the assembly
          /keyn[ame]:<text>         Key container name of key to sign assembly
          /main:<method>            Specifies the method name of the entry point
          /nologo                   Suppress the startup banner and copyright message
          /out:<filename>           Output file name for the assembly manifest
          /platform:<text>          Limit which platforms this code can run on; must be
                                    one of x86, ia64, amd64, or portable (the default)
          /prod[uct]:<text>         Product name
          /productv[ersion]:<text>  Product version
          /t[arget]:lib[rary]       Create a library
          /t[arget]:exe             Create a console executable
          /t[arget]:win[exe]        Create a Windows executable
          /template:<filename>      Specifies an assembly to get default options from
          /title:<text>             Title
          /trade[mark]:<text>       Trademark message
          /v[ersion]:<version>      Version (use * to auto-generate remaining numbers)
          /win32icon:<filename>     Use this icon for the output
          /win32res:<filename>      Specifies the Win32 resource file
        
        Sources: (at least one source input is required)
          <filename>[,<targetfile>] add file to assembly
          /embed[resource]:<filename>[,<name>[,Private]]
                                    embed the file as a resource in the assembly
          /link[resource]:<filename>[,<name>[,<targetfile>[,Private]]]
                                    link the file as a resource to the assembly

*/
        public string AlgorithmId
        {
            set { Bag["AlgorithmId"] = value; }
            get { return (string)Bag["AlgorithmId"]; }
        }

        public string BaseAddress
        {
            set { Bag["BaseAddress"] = value; }
            get { return (string)Bag["BaseAddress"]; }
        }

        public string CompanyName
        {
            set { Bag["CompanyName"] = value; }
            get { return (string)Bag["CompanyName"]; }
        }

        public string Configuration
        {
            set { Bag["Configuration"] = value; }
            get { return (string)Bag["Configuration"]; }
        }

        public string Copyright
        {
            set { Bag["Copyright"] = value; }
            get { return (string)Bag["Copyright"]; }
        }

        public string Culture
        {
            set { Bag["Culture"] = value; }
            get { return (string)Bag["Culture"]; }
        }

        public bool DelaySign
        {
            set { Bag["DelaySign"] = value; }
            get { return GetBoolParameterWithDefault("DelaySign", false); }
        }

        public string Description
        {
            set { Bag["Description"] = value; }
            get { return (string)Bag["Description"]; }
        }

        public string EvidenceFile
        {
            set { Bag["EvidenceFile"] = value; }
            get { return (string)Bag["EvidenceFile"]; }
        }

        public string FileVersion
        {
            set { Bag["FileVersion"] = value; }
            get { return (string)Bag["FileVersion"]; }
        }

        public string Flags
        {
            set { Bag["Flags"] = value; }
            get { return (string)Bag["Flags"]; }
        }

        public bool GenerateFullPaths
        {
            set { Bag["GenerateFullPaths"] = value; }
            get { return GetBoolParameterWithDefault("GenerateFullPaths", false); }
        }

        public string KeyFile
        {
            set { Bag["KeyFile"] = value; }
            get { return (string)Bag["KeyFile"]; }
        }

        public string KeyContainer
        {
            set { Bag["KeyContainer"] = value; }
            get { return (string)Bag["KeyContainer"]; }
        }

        public string MainEntryPoint
        {
            set { Bag["MainEntryPoint"] = value; }
            get { return (string)Bag["MainEntryPoint"]; }
        }

        [Output]
        [Required]
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

        public bool Prefer32Bit
        {
            set { Bag["Prefer32Bit"] = value; }
            get { return GetBoolParameterWithDefault("Prefer32Bit", false); }
        }

        public string ProductName
        {
            set { Bag["ProductName"] = value; }
            get { return (string)Bag["ProductName"]; }
        }

        public string ProductVersion
        {
            set { Bag["ProductVersion"] = value; }
            get { return (string)Bag["ProductVersion"]; }
        }

        public string[] ResponseFiles
        {
            set { Bag["ResponseFiles"] = value; }
            get { return (string[])Bag["ResponseFiles"]; }
        }

        public string TargetType
        {
            set { Bag["TargetType"] = value; }
            get { return (string)Bag["TargetType"]; }
        }

        public string TemplateFile
        {
            set { Bag["TemplateFile"] = value; }
            get { return (string)Bag["TemplateFile"]; }
        }

        public string Title
        {
            set { Bag["Title"] = value; }
            get { return (string)Bag["Title"]; }
        }

        public string Trademark
        {
            set { Bag["Trademark"] = value; }
            get { return (string)Bag["Trademark"]; }
        }

        public string Version
        {
            set { Bag["Version"] = value; }
            get { return (string)Bag["Version"]; }
        }

        public string Win32Icon
        {
            set { Bag["Win32Icon"] = value; }
            get { return (string)Bag["Win32Icon"]; }
        }

        public string Win32Resource
        {
            set { Bag["Win32Resource"] = value; }
            get { return (string)Bag["Win32Resource"]; }
        }


        // Input files: file[,target]
        // This is not required.
        public ITaskItem[] SourceModules
        {
            set { Bag["SourceModules"] = value; }
            get { return (ITaskItem[])Bag["SourceModules"]; }
        }

        // Embedded resource files: file[,name[,private]]
        public ITaskItem[] EmbedResources
        {
            set { Bag["EmbedResources"] = value; }
            get { return (ITaskItem[])Bag["EmbedResources"]; }
        }

        // Linked resource files: file[,name[,target][,private]]]
        public ITaskItem[] LinkResources
        {
            set { Bag["LinkResources"] = value; }
            get { return (ITaskItem[])Bag["LinkResources"]; }
        }

        public string SdkToolsPath
        {
            set { Bag["SdkToolsPath"] = value; }
            get { return (string)Bag["SdkToolsPath"]; }
        }

        #endregion

        #region Tool Members
        /// <summary>
        /// Return the name of the tool to execute.
        /// </summary>
        override protected string ToolName
        {
            get
            {
                return "al.exe";
            }
        }

        /// <summary>
        /// Return the path of the tool to execute
        /// </summary>
        override protected string GenerateFullPathToTool()
        {
            string pathToTool = null;

            // If COMPLUS_InstallRoot\COMPLUS_Version are set (the dogfood world), we want to find it there, instead of 
            // the SDK, which may or may not be installed. The following will look there.
            if (!String.IsNullOrEmpty(Environment.GetEnvironmentVariable("COMPLUS_InstallRoot")) || !String.IsNullOrEmpty(Environment.GetEnvironmentVariable("COMPLUS_Version")))
            {
                pathToTool = ToolLocationHelper.GetPathToDotNetFrameworkFile(ToolExe, TargetDotNetFrameworkVersion.Latest);
            }

            if (String.IsNullOrEmpty(pathToTool) || !File.Exists(pathToTool))
            {
                pathToTool = SdkToolsPathUtility.GeneratePathToTool(SdkToolsPathUtility.FileInfoExists, Microsoft.Build.Utilities.ProcessorArchitecture.CurrentProcessArchitecture, SdkToolsPath, ToolExe, Log, true);
            }

            return pathToTool;
        }

        /// <summary>
        /// Fills the provided CommandLineBuilderExtension with those switches and other information that can go into a response file.
        /// </summary>
        override protected internal void AddResponseFileCommands(CommandLineBuilderExtension commandLine)
        {
            commandLine.AppendSwitchIfNotNull("/algid:", this.AlgorithmId);
            commandLine.AppendSwitchIfNotNull("/baseaddress:", this.BaseAddress);
            commandLine.AppendSwitchIfNotNull("/company:", this.CompanyName);
            commandLine.AppendSwitchIfNotNull("/configuration:", this.Configuration);
            commandLine.AppendSwitchIfNotNull("/copyright:", this.Copyright);
            commandLine.AppendSwitchIfNotNull("/culture:", this.Culture);
            commandLine.AppendPlusOrMinusSwitch("/delaysign", this.Bag, "DelaySign");
            commandLine.AppendSwitchIfNotNull("/description:", this.Description);
            commandLine.AppendSwitchIfNotNull("/evidence:", this.EvidenceFile);
            commandLine.AppendSwitchIfNotNull("/fileversion:", this.FileVersion);
            commandLine.AppendSwitchIfNotNull("/flags:", this.Flags);
            commandLine.AppendWhenTrue("/fullpaths", this.Bag, "GenerateFullPaths");
            commandLine.AppendSwitchIfNotNull("/keyfile:", this.KeyFile);
            commandLine.AppendSwitchIfNotNull("/keyname:", this.KeyContainer);
            commandLine.AppendSwitchIfNotNull("/main:", this.MainEntryPoint);
            commandLine.AppendSwitchIfNotNull("/out:", (this.OutputAssembly == null) ? null : this.OutputAssembly.ItemSpec);
            commandLine.AppendSwitchIfNotNull("/platform:", this.PlatformWith32BitPreference);
            commandLine.AppendSwitchIfNotNull("/product:", this.ProductName);
            commandLine.AppendSwitchIfNotNull("/productversion:", this.ProductVersion);
            commandLine.AppendSwitchIfNotNull("/target:", this.TargetType);
            commandLine.AppendSwitchIfNotNull("/template:", this.TemplateFile);
            commandLine.AppendSwitchIfNotNull("/title:", this.Title);
            commandLine.AppendSwitchIfNotNull("/trademark:", this.Trademark);
            commandLine.AppendSwitchIfNotNull("/version:", this.Version);
            commandLine.AppendSwitchIfNotNull("/win32icon:", this.Win32Icon);
            commandLine.AppendSwitchIfNotNull("/win32res:", this.Win32Resource);

            commandLine.AppendSwitchIfNotNull("", this.SourceModules, new string[] { "TargetFile" });

            commandLine.AppendSwitchIfNotNull
            (
                "/embed:",
                this.EmbedResources,
                new string[] { "LogicalName", "Access" }
            );

            commandLine.AppendSwitchIfNotNull
            (
                "/link:",
                this.LinkResources,
                new string[] { "LogicalName", "TargetFile", "Access" }
            );

            // It's a good idea for the response file to be the very last switch passed, just 
            // from a predictability perspective.  This is also consistent with the compiler
            // tasks (Csc, etc.)
            if (this.ResponseFiles != null)
            {
                foreach (string responseFile in this.ResponseFiles)
                {
                    commandLine.AppendSwitchIfNotNull("@", responseFile);
                }
            }
        }

        public override bool Execute()
        {
            if (this.Culture != null && this.OutputAssembly != null)
            {
                // This allows subsequent tasks in the build process to know what culture each satellite 
                // assembly is associated with.
                this.OutputAssembly.SetMetadata("Culture", this.Culture);
            }

            return base.Execute();
        }

        #endregion
    }
}
