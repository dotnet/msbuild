// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.NET.Build.Containers;

namespace containerize;

internal class ContainerizeCommand : CliRootCommand
{
    internal CliArgument<DirectoryInfo> PublishDirectoryArgument { get; } = new CliArgument<DirectoryInfo>("PublishDirectory")
    {
        Description = "The directory for the build outputs to be published."
    }.AcceptExistingOnly();

    internal CliOption<string> BaseRegistryOption { get; } = new("--baseregistry")
    {
        Description = "The registry to use for the base image.",
        Required = true
    };

    internal CliOption<string> BaseImageNameOption { get; } = new("--baseimagename")
    {
        Description = "The base image to pull.",
        Required = true
    };

    internal CliOption<string> BaseImageTagOption { get; } = new("--baseimagetag")
    {
        Description = "The base image tag. Ex: 6.0",
        DefaultValueFactory = (_) => "latest"
    };

    internal CliOption<string> OutputRegistryOption { get; } = new("--outputregistry")
    {
        Description = "The registry to push to.",
        Required = false
    };

    internal CliOption<string> ArchiveOutputPathOption { get; } = new("--archiveoutputpath")
    {
        Description = "The file path to which to write a tar.gz archive of the container image.",
        Required = false
    };

    internal CliOption<string> RepositoryOption { get; } = new("--repository")
    {
        Description = "The name of the output container repository that will be pushed to the registry.",
        Required = true
    };

    internal CliOption<string[]> ImageTagsOption { get; } = new("--imagetags")
    {
        Description = "The tags to associate with the new image.",
        AllowMultipleArgumentsPerToken = true
    };

    internal CliOption<string> WorkingDirectoryOption { get; } = new("--workingdirectory")
    {
        Description = "The working directory of the container.",
        Required = true
    };

    internal CliOption<string[]> EntrypointOption { get; } = new("--entrypoint")
    {
        Description = "The entrypoint application of the container.",
        AllowMultipleArgumentsPerToken = true
    };

    internal CliOption<string[]> EntrypointArgsOption { get; } = new("--entrypointargs")
    {
        Description = "Arguments to pass alongside Entrypoint.",
        AllowMultipleArgumentsPerToken = true
    };

    internal CliOption<string[]> DefaultArgsOption { get; } = new CliOption<string[]>("--defaultargs")
    {
        Description = "Default arguments passed. These can be overridden by the user when the container is created.",
        AllowMultipleArgumentsPerToken = true
    };

    internal CliOption<string[]> AppCommandOption { get; } = new("--appcommand")
    {
        Description = "The file name and arguments that launch the application. For example: ['dotnet', 'app.dll'].",
        AllowMultipleArgumentsPerToken = true
    };

    internal CliOption<string[]> AppCommandArgsOption { get; } = new("--appcommandargs")
    {
        Description = "Arguments always passed to the application.",
        AllowMultipleArgumentsPerToken = true
    };

    internal CliOption<string> AppCommandInstructionOption { get; } = new CliOption<string>("--appcommandinstruction")
    {
        Description = "The Dockerfile instruction used for AppCommand. Can be set to 'DefaultArgs', 'Entrypoint', 'None', '' (default)."
    };

    internal CliOption<string> LocalRegistryOption { get; } = new CliOption<string>("--localregistry")
    {
        Description = "The local registry to push to."
    };

    internal CliOption<Dictionary<string, string>> LabelsOption { get; } = new("--labels")
    {
        Description = "Labels that the image configuration will include in metadata.",
        CustomParser = result => ParseDictionary(result, errorMessage: "Incorrectly formatted labels: "),
        AllowMultipleArgumentsPerToken = true
    };

    internal CliOption<Port[]> PortsOption { get; } = new("--ports")
    {
        Description = "Ports that the application declares that it will use. Note that this means nothing to container hosts, by default - it's mostly documentation. Ports should be of the form {number}/{type}, where {type} is tcp or udp",
        AllowMultipleArgumentsPerToken = true,
        CustomParser = result =>
        {
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
                else if (split.Length == 1)
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
                result.AddError(builder.ToString());
                return Array.Empty<Port>();
            }
            return goodPorts.ToArray();
        }
    };

    internal CliOption<Dictionary<string, string>> EnvVarsOption { get; } = new("--environmentvariables")
    {
        Description = "Container environment variables to set.",
        CustomParser = result => ParseDictionary(result, errorMessage: "Incorrectly formatted environment variables:  "),
        AllowMultipleArgumentsPerToken = true
    };

    internal CliOption<string> RidOption { get; } = new("--rid") { Description = "Runtime Identifier of the generated container." };

    internal CliOption<string> RidGraphPathOption { get; } = new("--ridgraphpath") { Description = "Path to the RID graph file." };

    internal CliOption<string> ContainerUserOption { get; } = new("--container-user") { Description = "User to run the container as." };

    internal ContainerizeCommand() : base("Containerize an application without Docker.")
    {
        PublishDirectoryArgument.AcceptLegalFilePathsOnly();
        Arguments.Add(PublishDirectoryArgument);
        Options.Add(BaseRegistryOption);
        Options.Add(BaseImageNameOption);
        Options.Add(BaseImageTagOption);
        Options.Add(OutputRegistryOption);
        Options.Add(ArchiveOutputPathOption);
        Options.Add(RepositoryOption);
        Options.Add(ImageTagsOption);
        Options.Add(WorkingDirectoryOption);
        Options.Add(EntrypointOption);
        Options.Add(EntrypointArgsOption);
        Options.Add(DefaultArgsOption);
        Options.Add(AppCommandOption);
        Options.Add(AppCommandArgsOption);
        Options.Add(AppCommandInstructionOption);
        Options.Add(LabelsOption);
        Options.Add(PortsOption);
        Options.Add(EnvVarsOption);
        Options.Add(RidOption);
        Options.Add(RidGraphPathOption);
        LocalRegistryOption.AcceptOnlyFromAmong(KnownLocalRegistryTypes.SupportedLocalRegistryTypes);
        Options.Add(LocalRegistryOption);
        Options.Add(ContainerUserOption);

        SetAction(async (parseResult, cancellationToken) =>
        {
            DirectoryInfo _publishDir = parseResult.GetValue(PublishDirectoryArgument)!;
            string _baseReg = parseResult.GetValue(BaseRegistryOption)!;
            string _baseName = parseResult.GetValue(BaseImageNameOption)!;
            string _baseTag = parseResult.GetValue(BaseImageTagOption)!;
            string? _outputReg = parseResult.GetValue(OutputRegistryOption);
            string? _archiveOutputPath = parseResult.GetValue(ArchiveOutputPathOption);
            string _name = parseResult.GetValue(RepositoryOption)!;
            string[] _tags = parseResult.GetValue(ImageTagsOption)!;
            string _workingDir = parseResult.GetValue(WorkingDirectoryOption)!;
            string[] _entrypoint = parseResult.GetValue(EntrypointOption) ?? Array.Empty<string>();
            string[] _entrypointArgs = parseResult.GetValue(EntrypointArgsOption) ?? Array.Empty<string>();
            string[] _defaultArgs = parseResult.GetValue(DefaultArgsOption) ?? Array.Empty<string>();
            string[] _appCommand = parseResult.GetValue(AppCommandOption) ?? Array.Empty<string>();
            string[] _appCommandArgs = parseResult.GetValue(AppCommandArgsOption) ?? Array.Empty<string>();
            string _appCommandInstruction = parseResult.GetValue(AppCommandInstructionOption) ?? "";
            Dictionary<string, string> _labels = parseResult.GetValue(LabelsOption) ?? new Dictionary<string, string>();
            Port[]? _ports = parseResult.GetValue(PortsOption);
            Dictionary<string, string> _envVars = parseResult.GetValue(EnvVarsOption) ?? new Dictionary<string, string>();
            string _rid = parseResult.GetValue(RidOption)!;
            string _ridGraphPath = parseResult.GetValue(RidGraphPathOption)!;
            string _localContainerDaemon = parseResult.GetValue(LocalRegistryOption)!;
            string? _containerUser = parseResult.GetValue(ContainerUserOption);

            //setup basic logging
            bool traceEnabled = Env.GetEnvironmentVariableAsBool("CONTAINERIZE_TRACE_LOGGING_ENABLED");
            LogLevel verbosity = traceEnabled ? LogLevel.Trace : LogLevel.Information;
            using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddSimpleConsole(c => c.ColorBehavior = LoggerColorBehavior.Disabled).SetMinimumLevel(verbosity));

            await ContainerBuilder.ContainerizeAsync(
                _publishDir,
                _workingDir,
                _baseReg,
                _baseName,
                _baseTag,
                _entrypoint,
                _entrypointArgs,
                _defaultArgs,
                _appCommand,
                _appCommandArgs,
                _appCommandInstruction,
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
                _archiveOutputPath,
                loggerFactory,
                cancellationToken).ConfigureAwait(false);
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
            argumentResult.AddError(errorMessage + invalidTokens.Aggregate((x, y) => x = x + ";" + y));
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
