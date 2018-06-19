// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

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
            set => Bag[nameof(AlgorithmId)] = value;
            get => (string)Bag[nameof(AlgorithmId)];
        }

        public string BaseAddress
        {
            set => Bag[nameof(BaseAddress)] = value;
            get => (string)Bag[nameof(BaseAddress)];
        }

        public string CompanyName
        {
            set => Bag[nameof(CompanyName)] = value;
            get => (string)Bag[nameof(CompanyName)];
        }

        public string Configuration
        {
            set => Bag[nameof(Configuration)] = value;
            get => (string)Bag[nameof(Configuration)];
        }

        public string Copyright
        {
            set => Bag[nameof(Copyright)] = value;
            get => (string)Bag[nameof(Copyright)];
        }

        public string Culture
        {
            set => Bag[nameof(Culture)] = value;
            get => (string)Bag[nameof(Culture)];
        }

        public bool DelaySign
        {
            set => Bag[nameof(DelaySign)] = value;
            get => GetBoolParameterWithDefault(nameof(DelaySign), false);
        }

        public string Description
        {
            set => Bag[nameof(Description)] = value;
            get => (string)Bag[nameof(Description)];
        }

        public string EvidenceFile
        {
            set => Bag[nameof(EvidenceFile)] = value;
            get => (string)Bag[nameof(EvidenceFile)];
        }

        public string FileVersion
        {
            set => Bag[nameof(FileVersion)] = value;
            get => (string)Bag[nameof(FileVersion)];
        }

        public string Flags
        {
            set => Bag["Flags"] = value;
            get => (string)Bag["Flags"];
        }

        public bool GenerateFullPaths
        {
            set => Bag[nameof(GenerateFullPaths)] = value;
            get => GetBoolParameterWithDefault(nameof(GenerateFullPaths), false);
        }

        public string KeyFile
        {
            set => Bag[nameof(KeyFile)] = value;
            get => (string)Bag[nameof(KeyFile)];
        }

        public string KeyContainer
        {
            set => Bag[nameof(KeyContainer)] = value;
            get => (string)Bag[nameof(KeyContainer)];
        }

        public string MainEntryPoint
        {
            set => Bag[nameof(MainEntryPoint)] = value;
            get => (string)Bag[nameof(MainEntryPoint)];
        }

        [Output]
        [Required]
        public ITaskItem OutputAssembly
        {
            set => Bag[nameof(OutputAssembly)] = value;
            get => (ITaskItem)Bag[nameof(OutputAssembly)];
        }

        public string Platform
        {
            set => Bag[nameof(Platform)] = value;
            get => (string)Bag[nameof(Platform)];
        }

        // Map explicit platform of "AnyCPU" or the default platform (null or ""), since it is commonly understood in the 
        // managed build process to be equivalent to "AnyCPU", to platform "AnyCPU32BitPreferred" if the Prefer32Bit 
        // property is set. 
        internal string PlatformWith32BitPreference
        {
            get
            {
                string platform = Platform;
                if ((String.IsNullOrEmpty(platform) || platform.Equals("anycpu", StringComparison.OrdinalIgnoreCase)) && Prefer32Bit)
                {
                    platform = "anycpu32bitpreferred";
                }
                return platform;
            }
        }

        public bool Prefer32Bit
        {
            set => Bag[nameof(Prefer32Bit)] = value;
            get => GetBoolParameterWithDefault(nameof(Prefer32Bit), false);
        }

        public string ProductName
        {
            set => Bag[nameof(ProductName)] = value;
            get => (string)Bag[nameof(ProductName)];
        }

        public string ProductVersion
        {
            set => Bag[nameof(ProductVersion)] = value;
            get => (string)Bag[nameof(ProductVersion)];
        }

        public string[] ResponseFiles
        {
            set => Bag[nameof(ResponseFiles)] = value;
            get => (string[])Bag[nameof(ResponseFiles)];
        }

        public string TargetType
        {
            set => Bag[nameof(TargetType)] = value;
            get => (string)Bag[nameof(TargetType)];
        }

        public string TemplateFile
        {
            set => Bag[nameof(TemplateFile)] = value;
            get => (string)Bag[nameof(TemplateFile)];
        }

        public string Title
        {
            set => Bag[nameof(Title)] = value;
            get => (string)Bag[nameof(Title)];
        }

        public string Trademark
        {
            set => Bag[nameof(Trademark)] = value;
            get => (string)Bag[nameof(Trademark)];
        }

        public string Version
        {
            set => Bag[nameof(Version)] = value;
            get => (string)Bag[nameof(Version)];
        }

        public string Win32Icon
        {
            set => Bag[nameof(Win32Icon)] = value;
            get => (string)Bag[nameof(Win32Icon)];
        }

        public string Win32Resource
        {
            set => Bag[nameof(Win32Resource)] = value;
            get => (string)Bag[nameof(Win32Resource)];
        }


        // Input files: file[,target]
        // This is not required.
        public ITaskItem[] SourceModules
        {
            set => Bag[nameof(SourceModules)] = value;
            get => (ITaskItem[])Bag[nameof(SourceModules)];
        }

        // Embedded resource files: file[,name[,private]]
        public ITaskItem[] EmbedResources
        {
            set => Bag[nameof(EmbedResources)] = value;
            get => (ITaskItem[])Bag[nameof(EmbedResources)];
        }

        // Linked resource files: file[,name[,target][,private]]]
        public ITaskItem[] LinkResources
        {
            set => Bag[nameof(LinkResources)] = value;
            get => (ITaskItem[])Bag[nameof(LinkResources)];
        }

        public string SdkToolsPath
        {
            set => Bag[nameof(SdkToolsPath)] = value;
            get => (string)Bag[nameof(SdkToolsPath)];
        }

        #endregion

        #region Tool Members
        /// <summary>
        /// Return the name of the tool to execute.
        /// </summary>
        protected override string ToolName => "al.exe";

        /// <summary>
        /// Return the path of the tool to execute
        /// </summary>
        protected override string GenerateFullPathToTool()
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
                pathToTool = SdkToolsPathUtility.GeneratePathToTool(SdkToolsPathUtility.FileInfoExists, ProcessorArchitecture.CurrentProcessArchitecture, SdkToolsPath, ToolExe, Log, true);
            }

            return pathToTool;
        }

        /// <summary>
        /// Fills the provided CommandLineBuilderExtension with those switches and other information that can go into a response file.
        /// </summary>
        protected internal override void AddResponseFileCommands(CommandLineBuilderExtension commandLine)
        {
            commandLine.AppendSwitchIfNotNull("/algid:", AlgorithmId);
            commandLine.AppendSwitchIfNotNull("/baseaddress:", BaseAddress);
            commandLine.AppendSwitchIfNotNull("/company:", CompanyName);
            commandLine.AppendSwitchIfNotNull("/configuration:", Configuration);
            commandLine.AppendSwitchIfNotNull("/copyright:", Copyright);
            commandLine.AppendSwitchIfNotNull("/culture:", Culture);
            commandLine.AppendPlusOrMinusSwitch("/delaysign", Bag, "DelaySign");
            commandLine.AppendSwitchIfNotNull("/description:", Description);
            commandLine.AppendSwitchIfNotNull("/evidence:", EvidenceFile);
            commandLine.AppendSwitchIfNotNull("/fileversion:", FileVersion);
            commandLine.AppendSwitchIfNotNull("/flags:", Flags);
            commandLine.AppendWhenTrue("/fullpaths", Bag, "GenerateFullPaths");
            commandLine.AppendSwitchIfNotNull("/keyfile:", KeyFile);
            commandLine.AppendSwitchIfNotNull("/keyname:", KeyContainer);
            commandLine.AppendSwitchIfNotNull("/main:", MainEntryPoint);
            commandLine.AppendSwitchIfNotNull("/out:", OutputAssembly?.ItemSpec);
            commandLine.AppendSwitchIfNotNull("/platform:", PlatformWith32BitPreference);
            commandLine.AppendSwitchIfNotNull("/product:", ProductName);
            commandLine.AppendSwitchIfNotNull("/productversion:", ProductVersion);
            commandLine.AppendSwitchIfNotNull("/target:", TargetType);
            commandLine.AppendSwitchIfNotNull("/template:", TemplateFile);
            commandLine.AppendSwitchIfNotNull("/title:", Title);
            commandLine.AppendSwitchIfNotNull("/trademark:", Trademark);
            commandLine.AppendSwitchIfNotNull("/version:", Version);
            commandLine.AppendSwitchIfNotNull("/win32icon:", Win32Icon);
            commandLine.AppendSwitchIfNotNull("/win32res:", Win32Resource);

            commandLine.AppendSwitchIfNotNull("", SourceModules, new[] { "TargetFile" });

            commandLine.AppendSwitchIfNotNull
            (
                "/embed:",
                EmbedResources,
                new[] { "LogicalName", "Access" }
            );

            commandLine.AppendSwitchIfNotNull
            (
                "/link:",
                LinkResources,
                new[] { "LogicalName", "TargetFile", "Access" }
            );

            // It's a good idea for the response file to be the very last switch passed, just 
            // from a predictability perspective.  This is also consistent with the compiler
            // tasks (Csc, etc.)
            if (ResponseFiles != null)
            {
                foreach (string responseFile in ResponseFiles)
                {
                    commandLine.AppendSwitchIfNotNull("@", responseFile);
                }
            }
        }

        public override bool Execute()
        {
            if (Culture != null)
            {
                // This allows subsequent tasks in the build process to know what culture each satellite 
                // assembly is associated with.
                OutputAssembly?.SetMetadata("Culture", Culture);
            }

            return base.Execute();
        }

        #endregion
    }
}
