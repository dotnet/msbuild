// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// The AspNetCompiler task, which is a wrapper around aspnet_compiler.exe
    /// </summary>
    public class AspNetCompiler : ToolTaskExtension
    {
        /*
            C:\WINDOWS\Microsoft.NET\Framework\v2.0.x86dbg>aspnet_compiler /?
            Utility to precompile an ASP.NET application
            Copyright (C) Microsoft Corporation. All rights reserved.

            Usage:
            aspnet_compiler [-?] [-m metabasePath | -v virtualPath [-p physicalDir]]
                            [[-u] [-f] [-d] targetDir] [-c] [-fixednames]
                            [-keyfile file | -keycontainer container [-aptca] [-delaySign]]

            -?            Prints this help text.
            -m            The full IIS metabase path of the application. This switch
                          cannot be combined with the -v or -p switches.
            -v            The virtual path of the application to be compiled (e.g.
                          "/MyApp"). If -p is specified, the physical path is used to
                          locate the application. Otherwise, the IIS metabase is used, and
                          the application is assumed to be in the default site (under
                          "/LM/W3SVC/1/Root"). This switch cannot be combined with the -m
                          switch.
            -p            The physical path of the application to be compiled. If -p is
                          missing, the IIS metabase is used to locate the app. This switch
                          must be combined with -v.
            -u            If specified, the precompiled application is updatable.
            -f            Overwrites the target directory if it already exists. Existing
                          contents are lost.
            -d            If specified, the debug information is emitted during
                          compilation.
            targetDir     The physical path to which the application is compiled. If not
                          specified, the application is precompiled in-place.
            -c            If specified, the precompiled application is fully rebuilt. Any
                          previously compiled components will be re-compiled. This option
                          is always enabled when targetDir is specified.
            -keyfile      The physical path to the strong name key file.
            -keycontainer Specifies a strong name key container.
            -aptca        If specified, the strong-name assembly will allow partially
                          trusted callers.
            -delaysign    If specified, the assemblly is not fully signed when created. 
            -fixednames   If specified, the compiled assemblies will be given fixed names.
            -nologo       Suppress compiler copyright message.

            Examples:

            The following two commands are equivalent, and rely on the IIS metabase. The
            compiled application is deployed to c:\MyTarget:
                aspnet_compiler -m /LM/W3SVC/1/Root/MyApp c:\MyTarget
                aspnet_compiler -v /MyApp c:\MyTarget

            The following command compiles the application /MyApp in-place. The effect is
            that no more compilations will be needed when HTTP requests are sent to it:
                aspnet_compiler -v /MyApp

            The following command does *not* rely on the IIS metabase, as it explicitly
            specifies the physical source directory of the application:
                aspnet_compiler -v /MyApp -p c:\myapp c:\MyTarget
        */

        /// <summary>
        /// If specified, the strong-name assembly will allow partially
        /// trusted callers.
        /// </summary>
        public bool AllowPartiallyTrustedCallers { get; set; }

        /// <summary>
        /// If specified, the assemblly is not fully signed when created. 
        /// </summary>
        public bool DelaySign { get; set; }

        /// <summary>
        /// If specified, the compiled assemblies will be given fixed names.
        /// </summary>
        public bool FixedNames { get; set; }

        /// <summary>
        /// Specifies a strong name key container.
        /// </summary>
        public string KeyContainer
        {
            get => (string)Bag[nameof(KeyContainer)];
            set => Bag[nameof(KeyContainer)] = value;
        }

        /// <summary>
        /// The physical path to the strong name key file.
        /// </summary>
        public string KeyFile
        {
            get => (string)Bag[nameof(KeyFile)];
            set => Bag[nameof(KeyFile)] = value;
        }

        /// <summary>
        /// The full IIS metabase path of the application. This switch 
        /// cannot be combined with the virtualPath or PhysicalDir option.
        /// </summary>
        public string MetabasePath
        {
            get => (string)Bag[nameof(MetabasePath)];
            set => Bag[nameof(MetabasePath)] = value;
        }

        /// <summary>
        /// The physical path of the application to be compiled. If physicalDir
        /// is missing, the IIS metabase is used to locate the application.
        /// </summary>
        public string PhysicalPath
        {
            get => (string)Bag[nameof(PhysicalPath)];
            set => Bag[nameof(PhysicalPath)] = value;
        }

        /// <summary>
        /// The physical path to which the application is compiled. If not
        /// specified, the application is precompiled in-place. 
        /// </summary>
        public string TargetPath
        {
            get => (string)Bag[nameof(TargetPath)];
            set => Bag[nameof(TargetPath)] = value;
        }

        /// <summary>
        /// The virtual path of the application to be compiled. If PhysicalDir is
        /// used to locate the application is specified. Otherwise, the IIS metabase
        /// is used, and the application is assumed to be in the default site (under
        /// "/LM/W3SVC/1/Root").
        /// </summary>
        public string VirtualPath
        {
            get => (string)Bag[nameof(VirtualPath)];
            set => Bag[nameof(VirtualPath)] = value;
        }

        /// <summary>
        /// If Updateable is true, then the web is compile with -u flag so that it
        /// can be updated after compilation
        /// </summary>
        public bool Updateable { get; set; }

        /// <summary>
        /// If Force is true, then the web is compile with -f flag overwriting
        /// files in the target location
        /// </summary>
        public bool Force { get; set; }

        /// <summary>
        /// If Debug is true, then the debug information will be emitted during
        /// compilation.
        /// </summary>
        public bool Debug { get; set; }

        /// <summary>
        /// If Clean is true, then the application will be built clean. Previously
        /// compiled components will be re-compiled.
        /// </summary>
        public bool Clean { get; set; }

        /// <summary>
        /// The TargetFrameworkMoniker indicating which .NET Framework version of 
        /// aspnet_compiler.exe should be used.  Only accepts .NET Framework monikers. 
        /// </summary>
        public string TargetFrameworkMoniker
        {
            get => (string)Bag[nameof(TargetFrameworkMoniker)];
            set => Bag[nameof(TargetFrameworkMoniker)] = value;
        }

        /// <summary>
        /// The name of the tool to execute
        /// </summary>
        protected override string ToolName => "aspnet_compiler.exe";

        /// <summary>
        /// Small helper property to get the "project name"
        /// </summary>
        private string ProjectName
        {
            get
            {
                if (PhysicalPath != null)
                {
                    return PhysicalPath;
                }
                return VirtualPath ?? MetabasePath;
            }
        }

        /// <summary>
        /// Small helper property for determining the "name of the target" that's currently being built
        /// </summary>
        private string TargetName
        {
            get
            {
                if (Clean)
                {
                    return "Clean";
                }

                // building the default target
                return null;
            }
        }

        /// <summary>
        /// Override the Execute method to be able to send ExternalProjectStarted/Finished events.
        /// </summary>
        /// <returns></returns>
        public override bool Execute()
        {
            Log.LogExternalProjectStarted(string.Empty, null, ProjectName, TargetName);
            bool succeeded = false;

            try
            {
                succeeded = base.Execute();
            }
            finally
            {
                Log.LogExternalProjectFinished(string.Empty, null, ProjectName, succeeded);
            }

            return succeeded;
        }

        /// <summary>
        /// Generates command line arguments for aspnet_compiler.exe
        /// </summary>
        /// <param name="commandLine">command line builder class to add arguments to</param>
        protected internal override void AddCommandLineCommands(CommandLineBuilderExtension commandLine)
        {
            commandLine.AppendSwitchIfNotNull("-m ", MetabasePath);
            commandLine.AppendSwitchIfNotNull("-v ", VirtualPath);
            commandLine.AppendSwitchIfNotNull("-p ", PhysicalPath);

            if (Updateable)
            {
                commandLine.AppendSwitch("-u");
            }

            if (Force)
            {
                commandLine.AppendSwitch("-f");
            }

            if (Clean)
            {
                commandLine.AppendSwitch("-c");
            }

            if (Debug)
            {
                commandLine.AppendSwitch("-d");
            }

            if (FixedNames)
            {
                commandLine.AppendSwitch("-fixednames");
            }

            commandLine.AppendSwitchIfNotNull("", TargetPath);

            if (AllowPartiallyTrustedCallers)
            {
                commandLine.AppendSwitch("-aptca");
            }

            if (DelaySign)
            {
                commandLine.AppendSwitch("-delaysign");
            }

            commandLine.AppendSwitchIfNotNull("-keyfile ", KeyFile);
            commandLine.AppendSwitchIfNotNull("-keycontainer ", KeyContainer);
        }

        /// <summary>
        /// Determine the path to aspnet_compiler.exe
        /// </summary>
        /// <returns>path to aspnet_compiler.exe, null if not found</returns>
        protected override string GenerateFullPathToTool()
        {
            // If ToolPath wasn't passed in, we want to default to the latest
            string pathToTool = ToolLocationHelper.GetPathToDotNetFrameworkFile(ToolExe, TargetDotNetFrameworkVersion.Latest);

            if (pathToTool == null)
            {
                Log.LogErrorWithCodeFromResources("General.FrameworksFileNotFound", ToolExe,
                    ToolLocationHelper.GetDotNetFrameworkVersionFolderPrefix(TargetDotNetFrameworkVersion.Latest));
            }

            return pathToTool;
        }

        /// <summary>
        /// Validate the task arguments, log any warnings/errors
        /// </summary>
        /// <returns>true if arguments are corrent enough to continue processing, false otherwise</returns>
        protected override bool ValidateParameters()
        {
            if (MetabasePath != null && (VirtualPath != null || PhysicalPath != null))
            {
                Log.LogErrorWithCodeFromResources("AspNetCompiler.CannotCombineMetabaseAndVirtualPathOrPhysicalPath");
                return false;
            }

            if (MetabasePath == null && VirtualPath == null)
            {
                Log.LogErrorWithCodeFromResources("AspNetCompiler.MissingMetabasePathAndVirtualPath");
                return false;
            }

            if (Updateable && TargetPath == null)
            {
                Log.LogErrorWithCodeFromResources("AspNetCompiler.MissingTargetPathForUpdatableApplication");
                return false;
            }

            if (Force && TargetPath == null)
            {
                Log.LogErrorWithCodeFromResources("AspNetCompiler.MissingTargetPathForOverwrittenApplication");
                return false;
            }

            return true;
        }
    }
}
