// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Common;
using Microsoft.Extensions.DependencyModel;

namespace Microsoft.DotNet.CommandFactory
{
    public class DepsJsonCommandResolver : ICommandResolver
    {
        private static readonly string[] s_extensionPreferenceOrder = new[]
        {
            "",
            ".exe",
            ".dll"
        };

        private string _nugetPackageRoot;
        private Muxer _muxer;

        public DepsJsonCommandResolver(string nugetPackageRoot)
            : this(new Muxer(), nugetPackageRoot) { }

        public DepsJsonCommandResolver(Muxer muxer, string nugetPackageRoot)
        {
            _muxer = muxer;
            _nugetPackageRoot = nugetPackageRoot;
        }

        public CommandSpec Resolve(CommandResolverArguments commandResolverArguments)
        {
            if (commandResolverArguments.CommandName == null
                || commandResolverArguments.DepsJsonFile == null)
            {
                return null;
            }

            return ResolveFromDepsJsonFile(
                commandResolverArguments.CommandName,
                commandResolverArguments.CommandArguments.OrEmptyIfNull(),
                commandResolverArguments.DepsJsonFile);
        }

        private CommandSpec ResolveFromDepsJsonFile(
            string commandName,
            IEnumerable<string> commandArgs,
            string depsJsonFile)
        {
            var dependencyContext = LoadDependencyContextFromFile(depsJsonFile);

            var commandPath = GetCommandPathFromDependencyContext(commandName, dependencyContext);
            if (commandPath == null)
            {
                return null;
            }

            return CreateCommandSpecUsingMuxerIfPortable(
                commandPath,
                commandArgs,
                depsJsonFile,
                _nugetPackageRoot,
                IsPortableApp(commandPath));
        }

        public DependencyContext LoadDependencyContextFromFile(string depsJsonFile)
        {
            DependencyContext dependencyContext = null;
            DependencyContextJsonReader contextReader = new DependencyContextJsonReader();

            using (var contextStream = File.OpenRead(depsJsonFile))
            {
                dependencyContext = contextReader.Read(contextStream);
            }

            return dependencyContext;
        }

        public string GetCommandPathFromDependencyContext(string commandName, DependencyContext dependencyContext)
        {
            var commandCandidates = new List<CommandCandidate>();

            var assemblyCommandCandidates = GetCommandCandidates(
                commandName,
                dependencyContext,
                CommandCandidateType.RuntimeCommandCandidate);
            var nativeCommandCandidates = GetCommandCandidates(
                commandName,
                dependencyContext,
                CommandCandidateType.NativeCommandCandidate);

            commandCandidates.AddRange(assemblyCommandCandidates);
            commandCandidates.AddRange(nativeCommandCandidates);

            var command = ChooseCommandCandidate(commandCandidates);

            return command?.GetAbsoluteCommandPath(_nugetPackageRoot);
        }

        private IEnumerable<CommandCandidate> GetCommandCandidates(
            string commandName,
            DependencyContext dependencyContext,
            CommandCandidateType commandCandidateType)
        {
            var commandCandidates = new List<CommandCandidate>();

            foreach (var runtimeLibrary in dependencyContext.RuntimeLibraries)
            {
                IEnumerable<RuntimeAssetGroup> runtimeAssetGroups = null;

                if (commandCandidateType == CommandCandidateType.NativeCommandCandidate)
                {
                    runtimeAssetGroups = runtimeLibrary.NativeLibraryGroups;
                }
                else if (commandCandidateType == CommandCandidateType.RuntimeCommandCandidate)
                {
                    runtimeAssetGroups = runtimeLibrary.RuntimeAssemblyGroups;
                }

                commandCandidates.AddRange(GetCommandCandidatesFromRuntimeAssetGroups(
                                    commandName,
                                    runtimeAssetGroups,
                                    runtimeLibrary.Name,
                                    runtimeLibrary.Version));
            }

            return commandCandidates;
        }

        private IEnumerable<CommandCandidate> GetCommandCandidatesFromRuntimeAssetGroups(
            string commandName,
            IEnumerable<RuntimeAssetGroup> runtimeAssetGroups,
            string PackageName,
            string PackageVersion)
        {
            var candidateAssetGroups = runtimeAssetGroups
                .Where(r => r.Runtime == string.Empty)
                .Where(a =>
                    a.AssetPaths.Any(p =>
                        Path.GetFileNameWithoutExtension(p).Equals(commandName, StringComparison.OrdinalIgnoreCase)));

            var commandCandidates = new List<CommandCandidate>();
            foreach (var candidateAssetGroup in candidateAssetGroups)
            {
                var candidateAssetPaths = candidateAssetGroup.AssetPaths.Where(
                    p => Path.GetFileNameWithoutExtension(p)
                    .Equals(commandName, StringComparison.OrdinalIgnoreCase));

                foreach (var candidateAssetPath in candidateAssetPaths)
                {
                    commandCandidates.Add(new CommandCandidate
                    {
                        PackageName = PackageName,
                        PackageVersion = PackageVersion,
                        RelativeCommandPath = candidateAssetPath
                    });
                }
            }

            return commandCandidates;
        }

        private CommandCandidate ChooseCommandCandidate(IEnumerable<CommandCandidate> commandCandidates)
        {
            foreach (var extension in s_extensionPreferenceOrder)
            {
                var candidate = commandCandidates
                    .FirstOrDefault(p => Path.GetExtension(p.RelativeCommandPath)
                        .Equals(extension, StringComparison.OrdinalIgnoreCase));

                if (candidate != null)
                {
                    return candidate;
                }
            }

            return null;
        }

        private CommandSpec CreateCommandSpecUsingMuxerIfPortable(
            string commandPath,
            IEnumerable<string> commandArgs,
            string depsJsonFile,
            string nugetPackagesRoot,
            bool isPortable)
        {
            var depsFileArguments = GetDepsFileArguments(depsJsonFile);
            var additionalProbingPathArguments = GetAdditionalProbingPathArguments();

            var muxerArgs = new List<string>();
            muxerArgs.Add("exec");
            muxerArgs.AddRange(depsFileArguments);
            muxerArgs.AddRange(additionalProbingPathArguments);
            muxerArgs.Add(commandPath);
            muxerArgs.AddRange(commandArgs);

            var escapedArgString = ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(muxerArgs);

            return new CommandSpec(_muxer.MuxerPath, escapedArgString);
        }

        private bool IsPortableApp(string commandPath)
        {
            var commandDir = Path.GetDirectoryName(commandPath);

            var runtimeConfigPath = Directory.EnumerateFiles(commandDir)
                .FirstOrDefault(x => x.EndsWith("runtimeconfig.json"));

            if (runtimeConfigPath == null)
            {
                return false;
            }

            var runtimeConfig = new RuntimeConfig(runtimeConfigPath);

            return runtimeConfig.IsPortable;
        }

        private IEnumerable<string> GetDepsFileArguments(string depsJsonFile)
        {
            return new[] { "--depsfile", depsJsonFile };
        }

        private IEnumerable<string> GetAdditionalProbingPathArguments()
        {
            return new[] { "--additionalProbingPath", _nugetPackageRoot };
        }

        private class CommandCandidate
        {
            public string PackageName { get; set; }
            public string PackageVersion { get; set; }
            public string RelativeCommandPath { get; set; }

            public string GetAbsoluteCommandPath(string nugetPackageRoot)
            {
                return Path.Combine(
                    nugetPackageRoot.Replace('/', Path.DirectorySeparatorChar),
                    PackageName.Replace('/', Path.DirectorySeparatorChar),
                    PackageVersion.Replace('/', Path.DirectorySeparatorChar),
                    RelativeCommandPath.Replace('/', Path.DirectorySeparatorChar));
            }
        }

        private enum CommandCandidateType
        {
            NativeCommandCandidate,
            RuntimeCommandCandidate
        }
    }
}
