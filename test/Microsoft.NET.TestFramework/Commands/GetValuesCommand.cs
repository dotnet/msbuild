using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.DotNet.Cli.Utils;
using System.Linq;
using System.IO;
using FluentAssertions;
using Microsoft.NET.TestFramework.Assertions;

namespace Microsoft.NET.TestFramework.Commands
{
    public sealed class GetValuesCommand : TestCommand
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

        public GetValuesCommand(MSBuildTest msbuild, string projectPath, string targetFramework,
            string valueName, ValueType valueType = ValueType.Property)
            : base(msbuild, projectPath)
        {
            _targetFramework = targetFramework;

            _valueName = valueName;
            _valueType = valueType;
        }

        public override CommandResult Execute(params string[] args)
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

            string linesAttribute = _valueType == ValueType.Property ?
                $"Lines=`$({_valueName})`" :
                $"Lines=`@({_valueName})`";

            string injectTargetContents =
@"<Project ToolsVersion=`14.0` xmlns=`http://schemas.microsoft.com/developer/msbuild/2003`>
  <PropertyGroup>
    <MSBuildAllProjects>$(MSBuildAllProjects);$(MSBuildThisFileFullPath)</MSBuildAllProjects>
  </PropertyGroup>
  <Target Name=`WriteValuesToFile` " + (ShouldCompile ? $"DependsOnTargets=`{DependsOnTargets}`" : "") + $@">
    <WriteLinesToFile
      File=`bin\$(Configuration)\$(TargetFramework)\{_valueName}Values.txt`
      {linesAttribute}
      Overwrite=`true`
      Encoding=`Unicode`
      />
  </Target>
</Project>";
            injectTargetContents = injectTargetContents.Replace('`', '"');

            File.WriteAllText(injectTargetPath, injectTargetContents);

            var outputDirectory = GetOutputDirectory(_targetFramework);
            outputDirectory.Create();

            var command = MSBuild.CreateCommandForTarget("WriteValuesToFile", newArgs.ToArray());

            return command.Execute();

        }

        public List<string> GetValues()
        {
            string outputFilename = $"{_valueName}Values.txt";
            var outputDirectory = GetOutputDirectory(_targetFramework, Configuration ?? "Debug");

            outputDirectory.Should().OnlyHaveFiles(new[] {
                outputFilename,
            });

            return File.ReadAllLines(Path.Combine(outputDirectory.FullName, outputFilename))
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrEmpty(line))
                .ToList();
        }
    }
}
