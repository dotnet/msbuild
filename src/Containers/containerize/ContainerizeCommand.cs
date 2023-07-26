// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Text;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.NET.Build.Containers;

namespace containerize;

internal class ContainerizeCommand : RootCommand
{
    internal Argument<DirectoryInfo> PublishDirectoryArgument { get; } = new Argument<DirectoryInfo>(
            name: "PublishDirectory",
            description: "The directory for the build outputs to be published.")
            .LegalFilePathsOnly().ExistingOnly();

    internal Option<string> BaseRegistryOption { get; } = new Option<string>(
            name: "--baseregistry",
            description: "The registry to use for the base image.")
            {
                IsRequired = true
            };

    internal Option<string> BaseImageNameOption { get;  } = new Option<string>(
            name: "--baseimagename",
            description: "The base image to pull.")
            {
                IsRequired = true
            };

    internal Option<string> BaseImageTagOption { get; } = new Option<string>(
            name: "--baseimagetag",
            description: "The base image tag. Ex: 6.0",
            getDefaultValue: () => "latest");

    internal Option<string> OutputRegistryOption { get; } = new Option<string>(
            name: "--outputregistry",
            description: "The registry to push to.")
            {
                IsRequired = false
            };

    internal Option<string> RepositoryOption { get; } = new Option<string>(
            name: "--repository",
            description: "The name of the output container repository that will be pushed to the registry.")
            {
                IsRequired = true
            };

    internal Option<string[]> ImageTagsOption { get; } = new Option<string[]>(
            name: "--imagetags",
            description: "The tags to associate with the new image.")
            {
                AllowMultipleArgumentsPerToken = true
            };

    internal Option<string> WorkingDirectoryOption { get; } = new Option<string>(
            name: "--workingdirectory",
            description: "The working directory of the container.")
            {
                IsRequired = true
            };

    internal Option<string[]> EntrypointOption { get; } = new Option<string[]>(
            name: "--entrypoint",
            description: "The entrypoint application of the container.")
            {
                IsRequired = true,
                AllowMultipleArgumentsPerToken = true
            };

    internal Option<string[]> EntrypointArgsOption { get; } = new Option<string[]>(
            name: "--entrypointargs",
            description: "Arguments to pass alongside Entrypoint.")
            {
                AllowMultipleArgumentsPerToken = true
            };

    internal Option<string> LocalContainerDaemonOption { get; } = new Option<string>(
            name: "--localcontainerdaemon",
            description: "The local daemon type to push to")
        .FromAmong(KnownDaemonTypes.SupportedLocalDaemonTypes);

    internal Option<Dictionary<string, string>> LabelsOption { get; } = new(
            name: "--labels",
            description: "Labels that the image configuration will include in metadata.",
            parseArgument: result => ParseDictionary(result, errorMessage: "Incorrectly formatted labels: "))
            {
                AllowMultipleArgumentsPerToken = true
            };

    internal Option<Port[]> PortsOption { get; } = new Option<Port[]>(
            name: "--ports",
            description: "Ports that the application declares that it will use. Note that this means nothing to container hosts, by default - it's mostly documentation. Ports should be of the form {number}/{type}, where {type} is tcp or udp",
            parseArgument: result => {
                string[] ports = result.Tokens.Select(x => x.Value).ToArray();
                var goodPorts = new List<Port>();
                var badPorts = new List<(string, ContainerHelpers.ParsePortError)>();

                foreach (string port in ports)
                {
                    string[] split = port.Split('/');
                    if (split.Length == 2)
                    {
                        if (ContainerHelpers.TryParsePort(split[0], split[1], out var portInfo, out var portError))
                        {
                            goodPorts.Add(portInfo.Value);
                        }
                        else
                        {
                            var pe = (ContainerHelpers.ParsePortError)portError!;
                            badPorts.Add((port, pe));
                        }
                    }
                    else if(split.Length == 1)
                    {
                        if (ContainerHelpers.TryParsePort(split[0], out var portInfo, out var portError))
                        {
                            goodPorts.Add(portInfo.Value);
                        }
                        else
                        {
                            var pe = (ContainerHelpers.ParsePortError)portError!;
                            badPorts.Add((port, pe));
                        }
                    }
                    else
                    {
                        badPorts.Add((port, ContainerHelpers.ParsePortError.UnknownPortFormat));
                        continue;
                    }
                }

                if (badPorts.Count != 0)
                {
                    var builder = new StringBuilder();
                    builder.AppendLine("Incorrectly formatted ports:");
                    foreach (var (badPort, error) in badPorts)
                    {
                        var errors = Enum.GetValues<ContainerHelpers.ParsePortError>().Where(e => error.HasFlag(e));
                        builder.AppendLine($"\t{badPort}:\t({string.Join(", ", errors)})");
                    }
                    result.ErrorMessage = builder.ToString();
                    return Array.Empty<Port>();
                }
                return goodPorts.ToArray();
            })
            {
                AllowMultipleArgumentsPerToken = true
            };

    internal Option<Dictionary<string, string>> EnvVarsOption { get; } = new(
            name: "--environmentvariables",
            description: "Container environment variables to set.",
            parseArgument: result => ParseDictionary(result, errorMessage: "Incorrectly formatted environment variables:  "))
            {
                AllowMultipleArgumentsPerToken = true
            };

    internal Option<string> RidOption { get; } = new Option<string>(name: "--rid", description: "Runtime Identifier of the generated container.");

    internal Option<string> RidGraphPathOption { get; } = new Option<string>(name: "--ridgraphpath", description: "Path to the RID graph file.");

    internal Option<string> ContainerUserOption { get; } = new Option<string>(name: "--container-user", description: "User to run the container as.");


    internal ContainerizeCommand() : base("Containerize an application without Docker.")
    {
        this.AddArgument(PublishDirectoryArgument);
        this.AddOption(BaseRegistryOption);
        this.AddOption(BaseImageNameOption);
        this.AddOption(BaseImageTagOption);
        this.AddOption(OutputRegistryOption);
        this.AddOption(RepositoryOption);
        this.AddOption(ImageTagsOption);
        this.AddOption(WorkingDirectoryOption);
        this.AddOption(EntrypointOption);
        this.AddOption(EntrypointArgsOption);
        this.AddOption(LabelsOption);
        this.AddOption(PortsOption);
        this.AddOption(EnvVarsOption);
        this.AddOption(RidOption);
        this.AddOption(RidGraphPathOption);
        this.AddOption(LocalContainerDaemonOption);
        this.AddOption(ContainerUserOption);

        this.SetHandler(async (context) =>
        {
            DirectoryInfo _publishDir = context.ParseResult.GetValueForArgument(PublishDirectoryArgument);
            string _baseReg = context.ParseResult.GetValueForOption(BaseRegistryOption)!;
            string _baseName = context.ParseResult.GetValueForOption(BaseImageNameOption)!;
            string _baseTag = context.ParseResult.GetValueForOption(BaseImageTagOption)!;
            string? _outputReg = context.ParseResult.GetValueForOption(OutputRegistryOption);
            string _name = context.ParseResult.GetValueForOption(RepositoryOption)!;
            string[] _tags = context.ParseResult.GetValueForOption(ImageTagsOption)!;
            string _workingDir = context.ParseResult.GetValueForOption(WorkingDirectoryOption)!;
            string[] _entrypoint = context.ParseResult.GetValueForOption(EntrypointOption)!;
            string[]? _entrypointArgs = context.ParseResult.GetValueForOption(EntrypointArgsOption);
            Dictionary<string, string> _labels = context.ParseResult.GetValueForOption(LabelsOption) ?? new Dictionary<string, string>();
            Port[]? _ports = context.ParseResult.GetValueForOption(PortsOption);
            Dictionary<string, string> _envVars = context.ParseResult.GetValueForOption(EnvVarsOption) ?? new Dictionary<string, string>();
            string _rid = context.ParseResult.GetValueForOption(RidOption)!;
            string _ridGraphPath = context.ParseResult.GetValueForOption(RidGraphPathOption)!;
            string _localContainerDaemon = context.ParseResult.GetValueForOption(LocalContainerDaemonOption)!;
            string? _containerUser = context.ParseResult.GetValueForOption(ContainerUserOption);

            //setup basic logging
            bool traceEnabled = Env.GetEnvironmentVariableAsBool("CONTAINERIZE_TRACE_LOGGING_ENABLED");
            LogLevel verbosity = traceEnabled ? LogLevel.Trace : LogLevel.Information;
            using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddSimpleConsole(c => c.ColorBehavior = LoggerColorBehavior.Disabled).SetMinimumLevel(verbosity));

            context.ExitCode = await ContainerBuilder.ContainerizeAsync(
                _publishDir,
                _workingDir,
                _baseReg,
                _baseName,
                _baseTag,
                _entrypoint,
                _entrypointArgs,
                _name,
                _tags,
                _outputReg,
                _labels,
                _ports,
                _envVars,
                _rid,
                _ridGraphPath,
                _localContainerDaemon,
                _containerUser,
                loggerFactory,
                context.GetCancellationToken()).ConfigureAwait(false);
        });
    }

    private static Dictionary<string, string> ParseDictionary(ArgumentResult argumentResult, string errorMessage)
    {
        Dictionary<string, string> parsed = new();
        string[] tokens = argumentResult.Tokens.Select(x => x.Value).ToArray();
        IEnumerable<string> invalidTokens = tokens.Where(v => v.Split('=', StringSplitOptions.TrimEntries).Length != 2);

        // Is there a non-zero number of Labels that didn't split into two elements? If so, assume invalid input and error out
        if (invalidTokens.Any())
        {
            argumentResult.ErrorMessage = errorMessage + invalidTokens.Aggregate((x, y) => x = x + ";" + y);
            return parsed;
        }

        foreach (string token in tokens)
        {
            string[] pair = token.Split('=', StringSplitOptions.TrimEntries);
            parsed[pair[0]] = pair[1];
        }
        return parsed;
    }
}
