// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Construction;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests.BackEnd;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Construction;

public sealed class ElementLocation_Tests(ITestOutputHelper output)
{
    private static readonly string s_pathToCommonTargets =
#if FEATURE_INSTALLED_MSBUILD
        Path.Combine(FrameworkLocationHelper.PathToDotNetFrameworkV45, "Microsoft.Common.targets");
#else
        Path.Combine(AppContext.BaseDirectory, "Microsoft.Common.targets");
#endif

    private readonly ITestOutputHelper _output = output;

    [Theory]
    [InlineData(0, 0, "FileOnly")]
    [InlineData(0, 1, "Small")]
    [InlineData(1, 0, "Small")]
    [InlineData(65_535, 65_535, "Small")]
    [InlineData(0, 65536, "Regular")]
    [InlineData(65536, 0, "Regular")]
    [InlineData(65536, 65536, "Regular")]
    public void CreateUsesExpectedRepresentation(int line, int column, string representation)
    {
        ElementLocation location = ElementLocation.Create("file", line, column);

        location.File.ShouldBe("file");
        location.Line.ShouldBe(line);
        location.Column.ShouldBe(column);
        location.GetType().Name.ShouldBe(representation);
    }

    [Fact]
    public void RegularAllowsEmptyFile()
    {
        ElementLocation location = ElementLocation.Create(string.Empty, 65536, 0);

        location.File.ShouldBeEmpty();
        location.Line.ShouldBe(65536);
        location.Column.ShouldBe(0);
        location.GetType().Name.ShouldBe("Regular");
    }

    [Fact]
    public void FileOnlyFactoryUsesFileOnlyRepresentation()
    {
        ElementLocation location = ElementLocation.Create("file");

        location.File.ShouldBe("file");
        location.Line.ShouldBe(0);
        location.Column.ShouldBe(0);
        location.GetType().Name.ShouldBe("FileOnly");
    }

    [Theory]
    [InlineData("file", 0, 0, "file")]
    [InlineData("file", 1, 0, "file (1)")]
    [InlineData("file", 1, 2, "file (1,2)")]
    [InlineData("file", 0, 2, "file")]
    public void LocationStringUsesAvailableCoordinates(string file, int line, int column, string expected)
        => ElementLocation.Create(file, line, column).LocationString.ShouldBe(expected);

    [Fact]
    public void EqualsUsesFileLineAndColumn()
    {
        ElementLocation location = ElementLocation.Create("file", 1, 2);

        location.Equals(location).ShouldBeTrue();
        location.Equals(ElementLocation.Create("FILE", 1, 2)).ShouldBeTrue();
        location.Equals(ElementLocation.Create("other", 1, 2)).ShouldBeFalse();
        location.Equals(ElementLocation.Create("file", 2, 2)).ShouldBeFalse();
        location.Equals(ElementLocation.Create("file", 1, 3)).ShouldBeFalse();
        location.Equals(null).ShouldBeFalse();
    }

    [Fact]
    public void EqualityIsLimitedToElementLocations()
    {
        var registryLocation = new RegistryLocation("registry");

        ElementLocation.Empty.Equals(registryLocation).ShouldBeFalse();
        registryLocation.Equals(ElementLocation.Empty).ShouldBeFalse();
    }

    [Fact]
    public void EqualLocationsHaveEqualHashCodes()
    {
        ElementLocation location1 = ElementLocation.Create("FILE", 1, 2);
        ElementLocation location2 = ElementLocation.Create("file", 1, 2);

        location1.Equals(location2).ShouldBeTrue();
        location1.GetHashCode().ShouldBe(location2.GetHashCode());
    }

    [Fact]
    public void ExternalSubclassUsesStructuralEquality()
    {
        ElementLocation location = ElementLocation.Create("FILE", 1, 2);
        ElementLocation externalLocation = new TestElementLocation("file", 1, 2);

        location.Equals(externalLocation).ShouldBeTrue();
        externalLocation.Equals(location).ShouldBeTrue();
        location.GetHashCode().ShouldBe(externalLocation.GetHashCode());
    }

    [Theory]
    [InlineData(-1, 2)]
    [InlineData(1, -2)]
    public void CreateRejectsNegativeCoordinates(int line, int column)
        => Should.Throw<InternalErrorException>(() => ElementLocation.Create("file", line, column));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void CreateWithoutFileReturnsEmpty(string? file)
    {
        ElementLocation.Create(file).ShouldBeSameAs(ElementLocation.Empty);
        ElementLocation.Create(file, 0, 0).ShouldBeSameAs(ElementLocation.Empty);
    }

    [Fact]
    public void EmptyLocationReturnsEmpty()
        => ElementLocation.EmptyLocation.ShouldBeSameAs(ElementLocation.Empty);

    [Fact]
    public void LargeColumnIsPreserved()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string content = ObjectModelHelpers.CleanupFileContents(
            $"""
            <Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
            <ItemGroup>{new string(' ', 70_000)}<x/></ItemGroup></Project>
            """);

        string file = env.CreateFile("large-column.proj", content).Path;

        InvalidProjectFileException exception = Should.Throw<InvalidProjectFileException>(() => ProjectRootElement.Open(file));

        exception.ColumnNumber.ShouldBe(70_012);
        exception.LineNumber.ShouldBe(2);
    }

    [Fact]
    public void LargeLineIsPreserved()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string content = ObjectModelHelpers.CleanupFileContents(
            $"""
            <Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
            <ItemGroup>{string.Concat(Enumerable.Repeat("\r\n", 70_000))} <x/></ItemGroup></Project>
            """);
        string file = env.CreateFile("large-line.proj", content).Path;

        InvalidProjectFileException exception = Should.Throw<InvalidProjectFileException>(() => ProjectRootElement.Open(file));

        exception.LineNumber.ShouldBe(70_002);
        exception.ColumnNumber.ShouldBe(2);
    }

    [Theory]
    [InlineData(null, 0, 0, "FileOnly")]
    [InlineData("file", 65_535, 2, "Small")]
    [InlineData("file", 65_536, 65_537, "Regular")]
    public void SerializationPreservesLocationAndRepresentation(string? file, int line, int column, string representation)
    {
        ElementLocation location = ElementLocation.Create(file, line, column);
        ElementLocation deserializedLocation = RoundTrip(location).ShouldNotBeNull();

        deserializedLocation.File.ShouldBe(location.File);
        deserializedLocation.Line.ShouldBe(location.Line);
        deserializedLocation.Column.ShouldBe(location.Column);
        deserializedLocation.GetType().Name.ShouldBe(representation);
    }

    [Fact]
    [Trait("Category", "netcore-osx-failing")]
    [Trait("Category", "netcore-linux-failing")]
    public void LocationStringsAreSameForReadOnlyAndWritableLoads()
    {
        string content = ObjectModelHelpers.CleanupFileContents("""
            <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                <UsingTask TaskName='t' AssemblyName='a' Condition='true'/>
                <UsingTask TaskName='t' AssemblyFile='a' Condition='true'/>
                <ItemDefinitionGroup Condition='true' Label='l'>
                    <m Condition='true'>  foo  bar
                    </m>
                </ItemDefinitionGroup>
                <ItemGroup>
                    <i Include='i' Condition='true' Exclude='r'>
                        <m Condition='true'/>
                    </i>
                </ItemGroup>
                <PropertyGroup>
                    <p Condition='true'/>
                </PropertyGroup>
                <!-- A comment -->
                <Target Name='Build' Condition='true' Inputs='i' Outputs='o'>
                    <ItemGroup>
                        <i Include='i' Condition='true' Exclude='r'>
                            <m Condition='true'/>
                        </i>
                        <i Remove='r'/>
                    </ItemGroup>
                    <PropertyGroup xml:space= 'preserve'>             <x/>
                        <p     Condition='true'/>
                    </PropertyGroup>
                    <Error Text='xyz' ContinueOnError='true' Importance='high'/>
                </Target>
                <Import Project='p' Condition='false'/>
            </Project>
            """);

        using TestEnvironment env = TestEnvironment.Create(_output);
        string readWriteLoadLocations = GetLocations(env, content, readOnly: false);
        string readOnlyLoadLocations = GetLocations(env, content, readOnly: true);

        _output.WriteLine(readWriteLoadLocations);

        Helpers.VerifyAssertLineByLine(readWriteLoadLocations, readOnlyLoadLocations);
    }

    [Fact]
    public void SaveToFileThrowsWhenReadOnly()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        XmlDocumentWithLocation document = LoadXmlDocumentWithLocation(s_pathToCommonTargets, readOnly: true);
        string outputFile = env.CreateFile("output.xml").Path;

        document.IsReadOnly.ShouldBeTrue();
        Should.Throw<InvalidOperationException>(() => document.Save(outputFile));
    }

    [Fact]
    [Trait("Category", "netcore-osx-failing")]
    [Trait("Category", "netcore-linux-failing")]
    public void SaveToStreamThrowsWhenReadOnly()
    {
        XmlDocumentWithLocation document = LoadXmlDocumentWithLocation(s_pathToCommonTargets, readOnly: true);
        using var stream = new MemoryStream();

        document.IsReadOnly.ShouldBeTrue();
        Should.Throw<InvalidOperationException>(() => document.Save(stream));
    }

    [Fact]
    [Trait("Category", "netcore-osx-failing")]
    [Trait("Category", "netcore-linux-failing")]
    public void SaveToTextWriterThrowsWhenReadOnly()
    {
        XmlDocumentWithLocation document = LoadXmlDocumentWithLocation(s_pathToCommonTargets, readOnly: true);
        using var writer = new StringWriter();

        document.IsReadOnly.ShouldBeTrue();
        Should.Throw<InvalidOperationException>(() => document.Save(writer));
    }

    [Fact]
    [Trait("Category", "netcore-osx-failing")]
    [Trait("Category", "netcore-linux-failing")]
    public void SaveToXmlWriterThrowsWhenReadOnly()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        XmlDocumentWithLocation document = LoadXmlDocumentWithLocation(s_pathToCommonTargets, readOnly: true);
        using XmlWriter writer = XmlWriter.Create(env.CreateFile("output.xml").Path);

        document.IsReadOnly.ShouldBeTrue();
        Should.Throw<InvalidOperationException>(() => document.Save(writer));
    }

    private static ElementLocation? RoundTrip(ElementLocation location)
    {
        IElementLocation translatableLocation = location;
        TranslationHelpers.GetWriteTranslator().Translate(ref translatableLocation, ElementLocation.FactoryForDeserialization);

        IElementLocation? deserializedLocation = null;
        TranslationHelpers.GetReadTranslator().Translate(ref deserializedLocation, ElementLocation.FactoryForDeserialization);

        return (ElementLocation?)deserializedLocation;
    }

    private static XmlDocumentWithLocation LoadXmlDocumentWithLocation(string file, bool readOnly)
    {
        var document = new XmlDocumentWithLocation(loadAsReadOnly: readOnly)
        {
            FullPath = file,
            XmlResolver = null,
        };

        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Ignore,
            XmlResolver = null,
        };

        using XmlReader reader = XmlReader.Create(file, settings);
        document.Load(reader);
        return document;
    }

    private static string GetLocations(TestEnvironment env, string content, bool readOnly)
    {
        string file = env.CreateFile($"locations-{readOnly}.proj", content).Path;
        XmlDocumentWithLocation document = LoadXmlDocumentWithLocation(file, readOnly);
        document.IsReadOnly.ShouldBe(readOnly);

        XmlNodeList? allNodes = document.SelectNodes("//*|//@*");
        allNodes.ShouldNotBeNull();

        var locations = new StringBuilder();
        foreach (XmlNode node in allNodes)
        {
            foreach (PropertyInfo property in node.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (property.Name == "Location" && property.GetValue(node) is ElementLocation location)
                {
                    locations.Append($"{node.Name}=={node.Value ?? string.Empty}: {location.LocationString}\r\n");
                }
            }
        }

        locations.Length.ShouldBeGreaterThan(0);
        return locations.ToString().Replace(file, @"c:\foo\bar.csproj");
    }

    private sealed class TestElementLocation(string file, int line, int column) : ElementLocation
    {
        public override string File { get; } = file;

        public override int Line { get; } = line;

        public override int Column { get; } = column;
    }
}
