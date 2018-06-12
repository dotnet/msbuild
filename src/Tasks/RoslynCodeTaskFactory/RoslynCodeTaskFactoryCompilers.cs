// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.IO;
using System.Linq;

namespace Microsoft.Build.Tasks
{
    internal abstract class RoslynCodeTaskFactoryCompilerBase : ToolTaskExtension
    {
#if RUNTIME_TYPE_NETCORE
        private static readonly string DotnetCliPath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
#endif

        private readonly Lazy<string> _executablePath;

        protected RoslynCodeTaskFactoryCompilerBase()
        {
            _executablePath = new Lazy<string>(() =>
            {
                string pathToBuildTools = ToolLocationHelper.GetPathToBuildTools(ToolLocationHelper.CurrentToolsVersion, DotNetFrameworkArchitecture.Bitness32);

                Func<string>[] possibleLocations =
                {
#if !RUNTIME_TYPE_NETCORE
                    // Full framework MSBuild
                    () => Path.Combine(pathToBuildTools, "Roslyn", ToolName),
#endif
#if RUNTIME_TYPE_NETCORE
                    // .NET Core 2.0+
                    () => Path.Combine(pathToBuildTools, "Roslyn", "bincore", Path.ChangeExtension(ToolName, ".dll")),
                    // Legacy .NET Core
                    () => Path.Combine(pathToBuildTools, "Roslyn", Path.ChangeExtension(ToolName, ".dll")),
#endif
                };

                return possibleLocations.Select(possibleLocation => possibleLocation()).FirstOrDefault(File.Exists);
            }, isThreadSafe: true);

            StandardOutputImportance = MessageImportance.Low.ToString("G");
        }

        public bool? Deterministic { get; set; }

        public bool? NoConfig { get; set; }

        public bool? NoLogo { get; set; }

        public bool? NoStandardLib { get; set; }

        public bool? Optimize { get; set; }

        public ITaskItem OutputAssembly { get; set; }

        public ITaskItem[] References { get; set; }

        public ITaskItem[] Sources { get; set; }

        public string TargetType { get; set; }

        public bool? UseSharedCompilation { get; set; }

        protected virtual string ReferenceSwitch => "/reference:";

        protected internal override void AddCommandLineCommands(CommandLineBuilderExtension commandLine)
        {
#if RUNTIME_TYPE_NETCORE
            commandLine.AppendFileNameIfNotNull(_executablePath.Value);
            commandLine.AppendTextUnquoted(" ");
#endif
            commandLine.AppendSwitchIfTrue("/noconfig", NoConfig);

            if (References != null)
            {
                foreach (ITaskItem reference in References)
                {
                    commandLine.AppendSwitchIfNotNull(ReferenceSwitch, reference.ItemSpec);
                }
            }

            commandLine.AppendPlusOrMinusSwitch("/deterministic", Deterministic);
            commandLine.AppendSwitchIfTrue("/nologo", NoLogo);
            commandLine.AppendPlusOrMinusSwitch("/optimize", Optimize);
            commandLine.AppendSwitchIfNotNull("/target:", TargetType);
            commandLine.AppendSwitchIfNotNull("/out:", OutputAssembly);
            commandLine.AppendFileNamesIfNotNull(Sources, " ");
        }

        protected override string GenerateFullPathToTool()
        {
            if (!String.IsNullOrWhiteSpace(ToolExe) && Path.IsPathRooted(ToolExe))
            {
                return ToolExe;
            }

#if RUNTIME_TYPE_NETCORE
            return DotnetCliPath;
#else
            return _executablePath.Value;
#endif
        }

        protected override void LogToolCommand(string message)
        {
            Log.LogMessageFromText(message, StandardOutputImportanceToUse);
        }
    }

    internal sealed class RoslynCodeTaskFactoryCSharpCompiler : RoslynCodeTaskFactoryCompilerBase
    {
        protected override string ToolName => "csc.exe";

        protected internal override void AddCommandLineCommands(CommandLineBuilderExtension commandLine)
        {
            base.AddCommandLineCommands(commandLine);

            commandLine.AppendPlusOrMinusSwitch("/nostdlib", NoStandardLib);
        }
    }

    internal sealed class RoslynCodeTaskFactoryVisualBasicCompiler : RoslynCodeTaskFactoryCompilerBase
    {
        public bool? OptionExplicit { get; set; }

        public string RootNamespace { get; set; }

        protected override string ToolName => "vbc.exe";

        protected internal override void AddCommandLineCommands(CommandLineBuilderExtension commandLine)
        {
            base.AddCommandLineCommands(commandLine);

            commandLine.AppendSwitchIfTrue("/nostdlib", NoStandardLib);
            commandLine.AppendPlusOrMinusSwitch("/optionexplicit", OptionExplicit);
            commandLine.AppendSwitchIfNotNull("/rootnamespace:", RootNamespace);
        }
    }
}
