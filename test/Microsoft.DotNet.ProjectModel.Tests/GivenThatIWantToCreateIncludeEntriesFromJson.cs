// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FluentAssertions;
using Microsoft.DotNet.ProjectModel.Files;
using Microsoft.DotNet.ProjectModel.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.DotNet.ProjectModel.Tests
{
    public class GivenThatIWantToCreateIncludeEntriesFromJson
    {
        private const string ProjectName = "some project name";
        private readonly string ProjectFilePath = PathUtility.EnsureTrailingSlash(AppContext.BaseDirectory);

        [Fact]
        public void PackInclude_is_null_when_it_is_not_set_in_the_ProjectJson()
        {
            var json = new JObject();
            var project = GetProject(json);

            project.PackOptions.PackInclude.Should().BeNull();
        }

        [Fact]
        public void It_sets_PackInclude_when_packInclude_is_set_in_the_ProjectJson()
        {
            const string somePackTarget = "some pack target";
            const string somePackValue = "ff/files/file1.txt";

            var json = JObject.Parse(string.Format(@"{{
'packOptions': {{
  'files': {{
    'mappings': {{
      '{0}': {{
        'includeFiles': '{1}'
      }}
    }}
  }}
}}}}", somePackTarget, somePackValue));

            CreateFile(somePackValue);
            var project = GetProject(json);

            var packInclude = GetIncludeFiles(project.PackOptions.PackInclude, "/").FirstOrDefault();

            packInclude.TargetPath.Should().Be(somePackTarget);
            packInclude.SourcePath.Should().Contain(PathUtility.GetPathWithDirectorySeparator(somePackValue));
        }

        [Fact]
        public void It_parses_compile_and_includes_files_successfully()
        {
            var json = JObject.Parse(@"{
'buildOptions': {
  'compile': {
    'includeFiles': [ 'files/file1.cs', 'files/file2.cs' ],
    'exclude': 'files/*ex.cs'
  }
}}");

            CreateFile("files/file1.cs");
            CreateFile("files/file2.cs");
            CreateFile("files/file1ex.cs");
            CreateFile("files/file2ex.cs");

            var project = GetProject(json);

            var compileInclude = GetIncludeFiles(project.GetCompilerOptions(null, null).CompileInclude, "/").ToArray();

            compileInclude.Should().HaveCount(2);

            compileInclude.Should().Contain(
                entry => entry.TargetPath == PathUtility.GetPathWithDirectorySeparator("files/file1.cs") &&
                    entry.SourcePath.Contains(PathUtility.GetPathWithDirectorySeparator("files/file1.cs")));

            compileInclude.Should().Contain(
                entry => entry.TargetPath == PathUtility.GetPathWithDirectorySeparator("files/file2.cs") &&
                    entry.SourcePath.Contains(PathUtility.GetPathWithDirectorySeparator("files/file2.cs")));
        }

        [Fact]
        public void It_parses_namedResources_successfully()
        {
            const string someString = "some string";
            const string someResourcePattern = "files/*.resx";

            var json = JObject.Parse(string.Format(@"{{
'buildOptions': {{
  'embed': {{
    'mappings': {{
      '{0}': {{
        'include': '{1}'
      }}
    }}
  }}
}}}}", someString, someResourcePattern));

            CreateFile("files/Resource.resx");

            var project = GetProject(json);

            var embedInclude = GetIncludeFiles(project.GetCompilerOptions(null, null).EmbedInclude, "/").FirstOrDefault();

            embedInclude.TargetPath.Should().Be(someString);
            embedInclude.SourcePath.Should().Contain(PathUtility.GetPathWithDirectorySeparator("files/Resource.resx"));
        }

        [Fact]
        public void It_parses_copyToOutput_and_includes_files_successfully()
        {
            var json = JObject.Parse(@"{
'buildOptions': {
  'copyToOutput': {
    'include': 'files/*.txt',
    'exclude': 'files/p*.txt',
    'excludeFiles': 'files/file1ex.txt',
  }
}}");

            CreateFile("files/file1.txt");
            CreateFile("files/file2.txt");
            CreateFile("files/file1ex.txt");

            var project = GetProject(json);

            var copyToOutputInclude = GetIncludeFiles(project.GetCompilerOptions(null, null).CopyToOutputInclude, "/").ToArray();

            copyToOutputInclude.Should().HaveCount(2);

            copyToOutputInclude.Should().Contain(
                entry => entry.TargetPath == PathUtility.GetPathWithDirectorySeparator("files/file1.txt") &&
                    entry.SourcePath.Contains(PathUtility.GetPathWithDirectorySeparator("files/file1.txt")));

            copyToOutputInclude.Should().Contain(
                entry => entry.TargetPath == PathUtility.GetPathWithDirectorySeparator("files/file2.txt") &&
                    entry.SourcePath.Contains(PathUtility.GetPathWithDirectorySeparator("files/file2.txt")));
        }

        [Fact]
        public void It_parses_PublishOptions_and_includes_files_successfully()
        {
            var json = JObject.Parse(@"{
'publishOptions': {
  'include': 'files/p*.txt',
  'exclude': 'files/*ex.txt',
  'includeFiles': 'files/pfile2ex.txt'
}}");

            CreateFile("files/pfile1.txt");
            CreateFile("files/pfile1ex.txt");
            CreateFile("files/pfile2ex.txt");

            var project = GetProject(json);

            var publishOptions = GetIncludeFiles(project.PublishOptions, "/").ToArray();

            publishOptions.Should().HaveCount(2);

            publishOptions.Should().Contain(
                entry => entry.TargetPath == PathUtility.GetPathWithDirectorySeparator("files/pfile1.txt") &&
                    entry.SourcePath.Contains(PathUtility.GetPathWithDirectorySeparator("files/pfile1.txt")));

            publishOptions.Should().Contain(
                entry => entry.TargetPath == PathUtility.GetPathWithDirectorySeparator("files/pfile2ex.txt") &&
                    entry.SourcePath.Contains(PathUtility.GetPathWithDirectorySeparator("files/pfile2ex.txt")));
        }

        private Project GetProject(JObject json, ProjectReaderSettings settings = null)
        {
            using (var stream = new MemoryStream())
            {
                using (var sw = new StreamWriter(stream, Encoding.UTF8, 256, true))
                {
                    using (var writer = new JsonTextWriter(sw))
                    {
                        writer.Formatting = Formatting.Indented;
                        json.WriteTo(writer);
                    }

                    stream.Position = 0;
                    var projectReader = new ProjectReader();
                    return projectReader.ReadProject(
                        stream,
                        ProjectName,
                        ProjectFilePath,
                        settings);
                }
            }
        }

        private IEnumerable<IncludeEntry> GetIncludeFiles(IncludeContext context, string targetBasePath)
        {
            return IncludeFilesResolver.GetIncludeFiles(context, targetBasePath, null);
        }

        private void CreateFile(string filePath)
        {
            filePath = Path.Combine(ProjectFilePath, filePath);
            var dirName = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(dirName))
            {
                Directory.CreateDirectory(dirName);
            }

            File.Create(filePath);
        }
    }
}
