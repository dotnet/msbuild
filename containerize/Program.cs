using System.CommandLine;
using Microsoft.NET.Build.Containers;
using System.Text.Json;
using System.CommandLine.Parsing;
using System.Text;

var publishDirectoryArg = new Argument<DirectoryInfo>(
    name: "PublishDirectory",
    description: "The directory for the build outputs to be published.")
    .LegalFilePathsOnly().ExistingOnly();

var baseRegistryOpt = new Option<string>(
    name: "--baseregistry",
    description: "The registry to use for the base image.")
{
    IsRequired = true
};

var baseImageNameOpt = new Option<string>(
    name: "--baseimagename",
    description: "The base image to pull.")
{
    IsRequired = true
};

var baseImageTagOpt = new Option<string>(
    name: "--baseimagetag",
    description: "The base image tag. Ex: 6.0",
    getDefaultValue: () => "latest");

var outputRegistryOpt = new Option<string>(
    name: "--outputregistry",
    description: "The registry to push to.")
{
    IsRequired = true
};

var imageNameOpt = new Option<string>(
    name: "--imagename",
    description: "The name of the output image that will be pushed to the registry.")
{
    IsRequired = true
};

var imageTagsOpt = new Option<string[]>(
    name: "--imagetags",
    description: "The tags to associate with the new image.");

var workingDirectoryOpt = new Option<string>(
    name: "--workingdirectory",
    description: "The working directory of the container.")
{
    IsRequired = true
};

var entrypointOpt = new Option<string[]>(
    name: "--entrypoint",
    description: "The entrypoint application of the container.")
{
    IsRequired = true
};

var entrypointArgsOpt = new Option<string[]>(
    name: "--entrypointargs",
    description: "Arguments to pass alongside Entrypoint.");

var labelsOpt = new Option<string[]>(
    name: "--labels",
    description: "Labels that the image configuration will include in metadata.",
    parseArgument: result =>
    {
        var labels = result.Tokens.Select(x => x.Value).ToArray();
        var badLabels = labels.Where((v) => v.Split('=').Length != 2);

        // Is there a non-zero number of Labels that didn't split into two elements? If so, assume invalid input and error out
        if (badLabels.Count() != 0)
        {
            result.ErrorMessage = "Incorrectly formatted labels: " + badLabels.Aggregate((x, y) => x = x + ";" + y);

            return new string[] { };
        }
        return labels;
    })
{
    AllowMultipleArgumentsPerToken = true
};

var portsOpt = new Option<Port[]>(
    name: "--ports",
    description: "Ports that the application declares that it will use. Note that this means nothing to container hosts, by default - it's mostly documentation. Ports should be of the form {number}/{type}, where {type} is tcp or udp",
    parseArgument: result => {
        var ports = result.Tokens.Select(x => x.Value).ToArray();
        var goodPorts = new List<Port>();
        var badPorts = new List<(string, ContainerHelpers.ParsePortError)>();
        
        foreach (var port in ports) {
            var split = port.Split('/');
            if (split.Length != 2) {
                badPorts.Add((port, ContainerHelpers.ParsePortError.UnknownPortFormat));
                continue;
            }
            if (ContainerHelpers.TryParsePort(split[0], split[1], out var portInfo, out var portError)) {
                goodPorts.Add(portInfo);
            } else {
                var pe = (ContainerHelpers.ParsePortError)portError!;
                badPorts.Add((port, pe));
            }
        }

        if (badPorts.Count() != 0)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Incorrectly formatted ports:");
            foreach (var (badPort, error) in badPorts){
                var errors = Enum.GetValues<ContainerHelpers.ParsePortError>().Where(e => error.HasFlag(e));
                builder.AppendLine($"\t{badPort}:\t({string.Join(", ", errors)})");
            }
            result.ErrorMessage = builder.ToString();
            return new Port[] { };
        }
        return goodPorts.ToArray();
    }
){
    AllowMultipleArgumentsPerToken = true
};

RootCommand root = new RootCommand("Containerize an application without Docker.")
{
    publishDirectoryArg,
    baseRegistryOpt,
    baseImageNameOpt,
    baseImageTagOpt,
    outputRegistryOpt,
    imageNameOpt,
    imageTagsOpt,
    workingDirectoryOpt,
    entrypointOpt,
    entrypointArgsOpt,
    labelsOpt,
    portsOpt
};

root.SetHandler(async (context) =>
{
    DirectoryInfo _publishDir = context.ParseResult.GetValueForArgument(publishDirectoryArg);
    string _baseReg = context.ParseResult.GetValueForOption(baseRegistryOpt) ?? "";
    string _baseName = context.ParseResult.GetValueForOption(baseImageNameOpt) ?? "";
    string _baseTag = context.ParseResult.GetValueForOption(baseImageTagOpt) ?? "";
    string _outputReg = context.ParseResult.GetValueForOption(outputRegistryOpt) ?? "";
    string _name = context.ParseResult.GetValueForOption(imageNameOpt) ?? "";
    string[] _tags = context.ParseResult.GetValueForOption(imageTagsOpt) ?? Array.Empty<string>();
    string _workingDir = context.ParseResult.GetValueForOption(workingDirectoryOpt) ?? "";
    string[] _entrypoint = context.ParseResult.GetValueForOption(entrypointOpt) ?? Array.Empty<string>();
    string[] _entrypointArgs = context.ParseResult.GetValueForOption(entrypointArgsOpt) ?? Array.Empty<string>();
    string[] _labels = context.ParseResult.GetValueForOption(labelsOpt) ?? Array.Empty<string>();
    Port[] _ports = context.ParseResult.GetValueForOption(portsOpt) ?? Array.Empty<Port>();
    await ContainerHelpers.Containerize(_publishDir, _workingDir, _baseReg, _baseName, _baseTag, _entrypoint, _entrypointArgs, _name, _tags, _outputReg, _labels, _ports);
});

return await root.InvokeAsync(args);