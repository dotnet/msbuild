using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.DotNet.Cli.Utils;
using System.Linq;
using System.IO;
using FluentAssertions;
using Microsoft.NET.TestFramework.Assertions;
using Xunit.Abstractions;

namespace Microsoft.NET.TestFramework.Commands
{
    public sealed class GetValuesCommand : MSBuildCommand
    {
        public enum ValueType
        {
            Property,
            Item
        }

        string _targetFramework;

        string _valueName;
        ValueType _valueType;

        public bool ShouldCompile { get; set; } = true;

        public string DependsOnTargets { get; set; } = "Compile";

        public string Configuration { get; set; }

        public List<string> MetadataNames { get; set; } = new List<string>();

        public GetValuesCommand(ITestOutputHelper log, string projectPath, string targetFramework,
            string valueName, ValueType valueType = ValueType.Property)
            : base(log, "WriteValuesToFile", projectPath, relativePathToProject: null)
        {
            _targetFramework = targetFramework;

            _valueName = valueName;
            _valueType = valueType;
        }

        protected override SdkCommandSpec CreateCommand(params string[] args)
        {
            var newArgs = new List<string>(args.Length + 2);
            newArgs.Add(FullPathProjectFile);
            newArgs.Add($"/p:ValueName={_valueName}");
            newArgs.AddRange(args);

            //  Override build target to write out DefineConstants value to a file in the output directory
            Directory.CreateDirectory(GetBaseIntermediateDirectory().FullName);
            string injectTargetPath = Path.Combine(
                GetBaseIntermediateDirectory().FullName,
                Path.GetFileName(ProjectFile) + ".WriteValuesToFile.g.targets");

            string linesAttribute;
            if (_valueType == ValueType.Property)
            {
                linesAttribute = $"$({_valueName})";
            }
            else
            {
                linesAttribute = $"%({_valueName}.Identity)";
                foreach (var metadataName in MetadataNames)
                {
                    linesAttribute += $"%09%({_valueName}.{metadataName})";
                }
            }

            string injectTargetContents =
$@"<Project ToolsVersion=`14.0` xmlns=`http://schemas.microsoft.com/developer/msbuild/2003`>
  <Target Name=`WriteValuesToFile` {(ShouldCompile ? $"DependsOnTargets=`{DependsOnTargets}`" : "")}>
    <ItemGroup>
      <LinesToWrite Include=`{linesAttribute}`/>
    </ItemGroup>
    <WriteLinesToFile
      File=`bin\$(Configuration)\$(TargetFramework)\{_valueName}Values.txt`
      Lines=`@(LinesToWrite)`
      Overwrite=`true`
      Encoding=`Unicode`
      />
  </Target>
</Project>";
            injectTargetContents = injectTargetContents.Replace('`', '"');

            File.WriteAllText(injectTargetPath, injectTargetContents);

            var outputDirectory = GetOutputDirectory(_targetFramework);
            outputDirectory.Create();

            return TestContext.Current.ToolsetUnderTest.CreateCommandForTarget("WriteValuesToFile", newArgs.ToArray());
        }

        public List<string> GetValues()
        {
            return GetValuesWithMetadata().Select(t => t.value).ToList();
        }

        public List<(string value, Dictionary<string, string> metadata)> GetValuesWithMetadata()
        {
            string outputFilename = $"{_valueName}Values.txt";
            var outputDirectory = GetOutputDirectory(_targetFramework, Configuration ?? "Debug");
            string fullFileName = Path.Combine(outputDirectory.FullName, outputFilename);

            if (File.Exists(fullFileName))
            {
                return File.ReadAllLines(fullFileName)
                   .Where(line => !string.IsNullOrWhiteSpace(line))
                   .Select(line =>
                   {
                       if (!MetadataNames.Any())
                       {
                           return (value: line, metadata: new Dictionary<string, string>());
                       }
                       else
                       {
                           var fields = line.Split('\t');

                           var dict = new Dictionary<string, string>();
                           for (int i = 0; i < MetadataNames.Count; i++)
                           {
                               dict[MetadataNames[i]] = fields[i + 1];
                           }

                           return (value: fields[0], metadata: dict);
                       }
                   })
                   .ToList();
            }
            else
            {
                return new List<(string value, Dictionary<string, string> metadata)>();
            }
        }
    }
}
