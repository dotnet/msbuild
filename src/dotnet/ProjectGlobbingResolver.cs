// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Microsoft.DotNet.ProjectModel;

namespace Microsoft.DotNet.Tools.Compiler
{
    internal class ProjectGlobbingResolver
    {
        internal IEnumerable<string> Resolve(IEnumerable<string> values)
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            if (!values.Any())
            {
                var fileName = Path.Combine(currentDirectory, Project.FileName);
                if (!File.Exists(fileName))
                {
                    throw new InvalidOperationException($"Couldn't find '{Project.FileName}' in current directory");
                }
                yield return fileName;
                yield break;
            }
            foreach (var value in values)
            {
                var fileName = Path.Combine(currentDirectory, value);
                if (File.Exists(fileName))
                {
                    yield return value;
                    continue;
                }

                fileName = Path.Combine(currentDirectory, value, Project.FileName);
                if (File.Exists(fileName))
                {
                    yield return fileName;
                    continue;
                }

                var matcher = new Matcher();
                matcher.AddInclude(value);
                var result = matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(currentDirectory)));
                if (result.Files.Any())
                {
                    foreach (var filePatternMatch in result.Files)
                    {
                        yield return filePatternMatch.Path;
                    }
                }
                else if (value.Contains("*"))
                {
                    throw new InvalidOperationException($"Globbing pattern '{value}' did not match any files");
                }
                else if (value.EndsWith(Project.FileName))
                {
                    throw new InvalidOperationException($"Could not find project file '{value}'");
                }
                else
                {
                    throw new InvalidOperationException($"Couldn't find '{Project.FileName}' in '{value}'");
                }
            }
        }
    }
}