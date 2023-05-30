// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.NET.Build.Containers;
using Xunit;

namespace containerize.UnitTests;

public class ParserTests
{
    [Fact]
    public void CanParseLabels()
    {
        ContainerizeCommand command = new();
        DirectoryInfo publishDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), DateTime.Now.ToString("yyyyMMddHHmmssfff"), nameof(CanParseLabels)));
        List<string> baseArgs = new()
        {
            publishDir.FullName,
            command.BaseRegistryOption.Aliases.First(),
            "MyBaseRegistry",
            command.BaseImageNameOption.Aliases.First(),
            "MyBaseImageName",
            command.RepositoryOption.Aliases.First(),
            "MyImageName",
            command.WorkingDirectoryOption.Aliases.First(),
            "MyWorkingDirectory",
            command.EntrypointOption.Aliases.First(),
            "MyEntryPoint"
        };

        baseArgs.Add(command.LabelsOption.Aliases.First());
        baseArgs.Add("NoValue=");
        baseArgs.Add("Valid2=Val2");
        baseArgs.Add("Valid3=Val 3");
        baseArgs.Add("Valid4=\"Val4\"");
        baseArgs.Add("Unbalanced1=\"Un1");
        baseArgs.Add("Unbalanced2=Un2\"");


        ParseResult parseResult = command.Parse(baseArgs.ToArray());

        Dictionary<string, string>? labels = parseResult.GetValueForOption(command.LabelsOption);

        Assert.NotNull(labels);
        Assert.Equal(6, labels.Count);
        Assert.Empty(labels["NoValue"]);
        Assert.Equal("Val2", labels["Valid2"]);
        Assert.Equal("Val 3", labels["Valid3"]);
        Assert.Equal("\"Val4\"", labels["Valid4"]);
        Assert.Equal("\"Un1", labels["Unbalanced1"]);
        Assert.Equal("Un2\"", labels["Unbalanced2"]);
    }

    [Fact]
    public void CanParseLabels2()
    {
        ContainerizeCommand command = new();
        DirectoryInfo publishDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), DateTime.Now.ToString("yyyyMMddHHmmssfff"), nameof(CanParseLabels)));
        List<string> baseArgs = new()
        {
            publishDir.FullName,
            command.BaseRegistryOption.Aliases.First(),
            "MyBaseRegistry",
            command.BaseImageNameOption.Aliases.First(),
            "MyBaseImageName",
            command.RepositoryOption.Aliases.First(),
            "MyImageName",
            command.WorkingDirectoryOption.Aliases.First(),
            "MyWorkingDirectory",
            command.EntrypointOption.Aliases.First(),
            "MyEntryPoint"
        };

        baseArgs.Add(command.LabelsOption.Aliases.First());
        baseArgs.Add("NoValue=");
        baseArgs.Add("Valid2=Val2");

        ParseResult parseResult = command.Parse(string.Join(" ", baseArgs));

        Dictionary<string, string>? labels = parseResult.GetValueForOption(command.LabelsOption);

        Assert.NotNull(labels);
        Assert.Equal(2, labels.Count);
        Assert.Empty(labels["NoValue"]);
        Assert.Equal("Val2", labels["Valid2"]);
    }

    [Theory]
    [InlineData("not-a-label")]
    [InlineData("not", "a", "label")]
    public void CanHandleInvalidLabels(params string[] labelStr)
    {
        ContainerizeCommand command = new();
        DirectoryInfo publishDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), DateTime.Now.ToString("yyyyMMddHHmmssfff"), nameof(CanParseLabels)));
        List<string> baseArgs = new()
        {
            publishDir.FullName,
            command.BaseRegistryOption.Aliases.First(),
            "MyBaseRegistry",
            command.BaseImageNameOption.Aliases.First(),
            "MyBaseImageName",
            command.RepositoryOption.Aliases.First(),
            "MyImageName",
            command.WorkingDirectoryOption.Aliases.First(),
            "MyWorkingDirectory",
            command.EntrypointOption.Aliases.First(),
            "MyEntryPoint"
        };

        baseArgs.Add(command.LabelsOption.Aliases.First());
        foreach (var label in labelStr)
        {
            baseArgs.Add(label);
        }

        ParseResult parseResult = command.Parse(baseArgs.ToArray());
        Assert.Equal(1, parseResult.Errors.Count);

        Assert.Equal($"Incorrectly formatted labels: {string.Join(";", labelStr)}", parseResult.Errors[0].Message);
    }

    [Fact]
    public void CanParseEnvironmentVariables()
    {
        ContainerizeCommand command = new();
        DirectoryInfo publishDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), DateTime.Now.ToString("yyyyMMddHHmmssfff"), nameof(CanParseEnvironmentVariables)));
        List<string> baseArgs = new()
        {
            publishDir.FullName,
            command.BaseRegistryOption.Aliases.First(),
            "MyBaseRegistry",
            command.BaseImageNameOption.Aliases.First(),
            "MyBaseImageName",
            command.RepositoryOption.Aliases.First(),
            "MyImageName",
            command.WorkingDirectoryOption.Aliases.First(),
            "MyWorkingDirectory",
            command.EntrypointOption.Aliases.First(),
            "MyEntryPoint"
        };

        baseArgs.Add(command.EnvVarsOption.Aliases.First());
        baseArgs.Add("NoValue=");
        baseArgs.Add("Valid2=Val2");
        baseArgs.Add("Valid3=Val 3");
        baseArgs.Add("Valid4=\"Val4\"");
        baseArgs.Add("Unbalanced1=\"Un1");
        baseArgs.Add("Unbalanced2=Un2\"");


        ParseResult parseResult = command.Parse(baseArgs.ToArray());
        Assert.Empty(parseResult.Errors);

        Dictionary<string, string>? envVars = parseResult.GetValueForOption(command.EnvVarsOption);

        Assert.NotNull(envVars);
        Assert.Equal(6, envVars.Count);
        Assert.Empty(envVars["NoValue"]);
        Assert.Equal("Val2", envVars["Valid2"]);
        Assert.Equal("Val 3", envVars["Valid3"]);
        Assert.Equal("\"Val4\"", envVars["Valid4"]);
        Assert.Equal("\"Un1", envVars["Unbalanced1"]);
        Assert.Equal("Un2\"", envVars["Unbalanced2"]);
    }

    [Fact]
    public void CanParsePorts()
    {
        ContainerizeCommand command = new();
        DirectoryInfo publishDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), DateTime.Now.ToString("yyyyMMddHHmmssfff"), nameof(CanParsePorts)));
        List<string> baseArgs = new()
        {
            publishDir.FullName,
            command.BaseRegistryOption.Aliases.First(),
            "MyBaseRegistry",
            command.BaseImageNameOption.Aliases.First(),
            "MyBaseImageName",
            command.RepositoryOption.Aliases.First(),
            "MyImageName",
            command.WorkingDirectoryOption.Aliases.First(),
            "MyWorkingDirectory",
            command.EntrypointOption.Aliases.First(),
            "MyEntryPoint"
        };

        baseArgs.Add(command.PortsOption.Aliases.First());
        baseArgs.Add("1500");
        baseArgs.Add("1501/udp");
        baseArgs.Add("1501/tcp");
        baseArgs.Add("1502");


        ParseResult parseResult = command.Parse(baseArgs.ToArray());
        Assert.Empty(parseResult.Errors);

        Port[]? ports = parseResult.GetValueForOption(command.PortsOption);

        Assert.NotNull(ports);
        Assert.Equal(4, ports.Length);
        Assert.Contains(new Port(1500, PortType.tcp), ports);
        Assert.Contains(new Port(1501, PortType.udp), ports);
        Assert.Contains(new Port(1501, PortType.tcp), ports);
        Assert.Contains(new Port(1502, PortType.tcp), ports);
    }

    [Theory]
    [InlineData("1501/smth", "(MissingPortNumber, InvalidPortType)")]
    [InlineData("1501\\tcp", "(MissingPortNumber, InvalidPortNumber)")]
    [InlineData("not-a-number", "(MissingPortNumber, InvalidPortNumber)")]
    public void CanHandleInvalidPorts(string portStr, string reason)
    {
        string errorMessage = $"Incorrectly formatted ports:{Environment.NewLine}\t{portStr}:\t{reason}{Environment.NewLine}";

        ContainerizeCommand command = new();
        DirectoryInfo publishDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), DateTime.Now.ToString("yyyyMMddHHmmssfff"), nameof(CanParsePorts)));
        List<string> baseArgs = new()
        {
            publishDir.FullName,
            command.BaseRegistryOption.Aliases.First(),
            "MyBaseRegistry",
            command.BaseImageNameOption.Aliases.First(),
            "MyBaseImageName",
            command.RepositoryOption.Aliases.First(),
            "MyImageName",
            command.WorkingDirectoryOption.Aliases.First(),
            "MyWorkingDirectory",
            command.EntrypointOption.Aliases.First(),
            "MyEntryPoint"
        };

        baseArgs.Add(command.PortsOption.Aliases.First());
        baseArgs.Add(portStr);

        ParseResult parseResult = command.Parse(baseArgs.ToArray());
        Assert.Equal(1, parseResult.Errors.Count);

        Assert.Equal(errorMessage, parseResult.Errors[0].Message);
    }
}

