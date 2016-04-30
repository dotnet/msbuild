// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.InternalAbstractions;
using Microsoft.DotNet.ProjectModel;
using NuGet.Frameworks;

// This class is responsible with defining the arguments for the Compile verb.
// It knows how to interpret them and set default values
namespace Microsoft.DotNet.Tools.Compiler
{
    public delegate bool OnExecute(IEnumerable<string> files, IEnumerable<NuGetFramework> frameworks, BuildCommandApp buildCommand);

    public class BuildCommandApp
    {
        public static readonly string NoIncrementalFlag = "--no-incremental";
        public static readonly string BuildProfileFlag = "--build-profile";

        private readonly CommandLineApplication _app;

        // options and arguments for compilation
        private CommandOption _outputOption;
        private CommandOption _buildBasePath;
        private CommandOption _frameworkOption;
        private CommandOption _runtimeOption;
        private CommandOption _versionSuffixOption;
        private CommandOption _configurationOption;
        private CommandArgument _projectArgument;

        private CommandOption _shouldPrintIncrementalPreconditionsArgument;
        private CommandOption _shouldNotUseIncrementalityArgument;
        private CommandOption _shouldSkipDependenciesArgument;


        public string BuildBasePathValue { get; set; }
        public string RuntimeValue { get; set; }
        public string OutputValue { get; set; }
        public string VersionSuffixValue { get; set; }
        public string ConfigValue { get; set; }
        public bool IsNativeValue { get; set; }
        public bool ShouldPrintIncrementalPreconditions { get; set; }
        public bool ShouldNotUseIncrementality { get; set; }
        public bool ShouldSkipDependencies { get; set; }

        public WorkspaceContext Workspace { get; private set; }

        // workaround: CommandLineApplication is internal therefore I cannot make _app protected so baseclasses can add their own params
        private readonly Dictionary<string, CommandOption> baseClassOptions;

        public BuildCommandApp(string name, string fullName, string description) : this(name, fullName, description, workspace: null) { }

        public BuildCommandApp(string name, string fullName, string description, WorkspaceContext workspace)
        {
            Workspace = workspace;
            _app = new CommandLineApplication
            {
                Name = name,
                FullName = fullName,
                Description = description
            };

            baseClassOptions = new Dictionary<string, CommandOption>();

            AddCompileParameters();
        }

        private void AddCompileParameters()
        {
            _app.HelpOption("-h|--help");

            _outputOption = _app.Option("-o|--output <OUTPUT_DIR>", "Directory in which to place outputs", CommandOptionType.SingleValue);
            _buildBasePath = _app.Option("-b|--build-base-path <OUTPUT_DIR>", "Directory in which to place temporary outputs", CommandOptionType.SingleValue);
            _frameworkOption = _app.Option("-f|--framework <FRAMEWORK>", "Compile a specific framework", CommandOptionType.SingleValue);
            _runtimeOption = _app.Option("-r|--runtime <RUNTIME_IDENTIFIER>", "Produce runtime-specific assets for the specified runtime", CommandOptionType.SingleValue);
            _configurationOption = _app.Option("-c|--configuration <CONFIGURATION>", "Configuration under which to build", CommandOptionType.SingleValue);
            _versionSuffixOption = _app.Option("--version-suffix <VERSION_SUFFIX>", "Defines what `*` should be replaced with in version field in project.json", CommandOptionType.SingleValue);
            _projectArgument = _app.Argument("<PROJECT>", "The project to compile, defaults to the current directory. " +
                                                          "Can be one or multiple paths to project.json, project directory " +
                                                          "or globbing patter that matches project.json files", multipleValues: true);

            _shouldPrintIncrementalPreconditionsArgument = _app.Option(BuildProfileFlag, "Set this flag to print the incremental safety checks that prevent incremental compilation", CommandOptionType.NoValue);
            _shouldNotUseIncrementalityArgument = _app.Option(NoIncrementalFlag, "Set this flag to turn off incremental build", CommandOptionType.NoValue);
            _shouldSkipDependenciesArgument = _app.Option("--no-dependencies", "Set this flag to ignore project to project references and only build the root project", CommandOptionType.NoValue);
        }

        public int Execute(OnExecute execute, string[] args)
        {
            _app.OnExecute(() =>
            {
                if (_outputOption.HasValue() && !_frameworkOption.HasValue())
                {
                    Reporter.Error.WriteLine("When the '--output' option is provided, the '--framework' option must also be provided.");
                    return 1;
                }

                OutputValue = _outputOption.Value();
                BuildBasePathValue = _buildBasePath.Value();
                ConfigValue = _configurationOption.Value() ?? Constants.DefaultConfiguration;
                RuntimeValue = _runtimeOption.Value();
                VersionSuffixValue = _versionSuffixOption.Value();
                ShouldPrintIncrementalPreconditions = _shouldPrintIncrementalPreconditionsArgument.HasValue();
                ShouldNotUseIncrementality = _shouldNotUseIncrementalityArgument.HasValue();
                ShouldSkipDependencies = _shouldSkipDependenciesArgument.HasValue();

                // Set defaults based on the environment
                if (Workspace == null)
                {
                    var settings = ProjectReaderSettings.ReadFromEnvironment();

                    if (!string.IsNullOrEmpty(VersionSuffixValue))
                    {
                        settings.VersionSuffix = VersionSuffixValue;
                    }
                    Workspace = WorkspaceContext.Create(settings, designTime: false);
                }

                var files = new ProjectGlobbingResolver().Resolve(_projectArgument.Values);
                IEnumerable<NuGetFramework> frameworks = null;
                if (_frameworkOption.HasValue())
                {
                    frameworks = new[] { NuGetFramework.Parse(_frameworkOption.Value()) };
                }
                var success = execute(files, frameworks, this);

                return success ? 0 : 1;
            });

            return _app.Execute(args);
        }

        public IEnumerable<string> GetRuntimes()
        {
            var rids = new List<string>();
            if (string.IsNullOrEmpty(RuntimeValue))
            {
                return RuntimeEnvironmentRidExtensions.GetAllCandidateRuntimeIdentifiers();
            }
            else
            {
                return new[] { RuntimeValue };
            }
        }
    }
}
