// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using System.Collections.Generic;
using Xunit.Abstractions;

namespace Microsoft.NET.TestFramework
{
    public class TestAsset : TestDirectory
    {
        private readonly string _testAssetRoot;

        private List<string> _projectFiles;

        public string TestRoot => Path;

        internal TestAsset(string testDestination, string sdkVersion) : base(testDestination, sdkVersion)
        {
        }

        internal TestAsset(string testAssetRoot, string testDestination, string sdkVersion) : base(testDestination, sdkVersion)
        {
            if (string.IsNullOrEmpty(testAssetRoot))
            {
                throw new ArgumentException("testAssetRoot");
            }

            _testAssetRoot = testAssetRoot;
        }

        internal void FindProjectFiles()
        {
            _projectFiles = new List<string>();

            var files = Directory.GetFiles(base.Path, "*.*", SearchOption.AllDirectories);

            foreach (string file in files)
            {
                if (System.IO.Path.GetFileName(file).EndsWith("proj"))
                {
                    _projectFiles.Add(file);
                }
            }
        }

        public TestAsset WithSource()
        {
            _projectFiles = new List<string>();

            var sourceDirs = Directory.GetDirectories(_testAssetRoot, "*", SearchOption.AllDirectories)
              .Where(dir => !IsBinOrObjFolder(dir));

            foreach (string sourceDir in sourceDirs)
            {
                Directory.CreateDirectory(sourceDir.Replace(_testAssetRoot, Path));
            }

            var sourceFiles = Directory.GetFiles(_testAssetRoot, "*.*", SearchOption.AllDirectories)
                                  .Where(file =>
                                  {
                                      return !IsInBinOrObjFolder(file);
                                  });

            foreach (string srcFile in sourceFiles)
            {
                string destFile = srcFile.Replace(_testAssetRoot, Path);
                // For project.json, we need to replace the version of the Microsoft.DotNet.Core.Sdk with the actual build version
                if (System.IO.Path.GetFileName(srcFile).EndsWith("proj"))
                {
                    var project = XDocument.Load(srcFile);

                    using (var file = File.CreateText(destFile))
                    {
                        project.Save(file);
                    }

                    _projectFiles.Add(destFile);
                }
                else
                {
                    File.Copy(srcFile, destFile, true);
                }
            }

            return this;
        }

        public TestAsset WithTargetFramework(string targetFramework, string projectName = null)
        {
            return WithTargetFramework(
            p =>
            {
                var ns = p.Root.Name.Namespace;
                p.Root.Elements(ns + "PropertyGroup").Elements(ns + "TargetFramework").Single().SetValue(targetFramework);
            },
            targetFramework,
            projectName);
        }

        public TestAsset WithTargetFrameworks(string targetFrameworks, string projectName = null)
        {
            return WithTargetFramework(
            p =>
            {
                var ns = p.Root.Name.Namespace;
                var propertyGroup = p.Root.Elements(ns + "PropertyGroup").First();
                propertyGroup.Elements(ns + "TargetFramework").SingleOrDefault()?.Remove();
                propertyGroup.Add(new XElement(ns + "TargetFramework", targetFrameworks));
            },
            targetFrameworks,
            projectName);
        }

        public TestAsset WithTargetFrameworkOrFrameworks(string targetFrameworkOrFrameworks, bool multitarget, string projectName = null)
        {
            if (multitarget)
            {
                return WithTargetFrameworks(targetFrameworkOrFrameworks, projectName);
            }
            else
            {
                return WithTargetFramework(targetFrameworkOrFrameworks, projectName);
            }
        }

        private TestAsset WithTargetFramework(Action<XDocument> actionOnProject, string targetFramework, string projectName = null)
        {
            if (string.IsNullOrEmpty(targetFramework))
            {
                return this;
            }

            return WithProjectChanges((path, project) =>
            {
                if (!string.IsNullOrEmpty(projectName))
                {
                    if (!projectName.Equals(System.IO.Path.GetFileNameWithoutExtension(path), StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }
                }

                var ns = project.Root.Name.Namespace;
                actionOnProject(project);
            });
        }

        public TestAsset WithProjectChanges(Action<XDocument> xmlAction)
        {
            return WithProjectChanges((path, project) => xmlAction(project));
        }

        public TestAsset WithProjectChanges(Action<string, XDocument> xmlAction)
        {
            if (_projectFiles == null)
            {
                FindProjectFiles();
            }

            foreach (var projectFile in _projectFiles)
            {
                var project = XDocument.Load(projectFile);

                xmlAction(projectFile, project);

                using (var file = File.CreateText(projectFile))
                {
                    project.Save(file);
                }
            }
            return this;
            
        }

        public RestoreCommand GetRestoreCommand(ITestOutputHelper log, string relativePath = "")
        {
            return new RestoreCommand(log, System.IO.Path.Combine(TestRoot, relativePath));
        }

        public NuGetRestoreCommand GetNuGetRestoreCommand(ITestOutputHelper log, string relativePath = "")
        {
            return new NuGetRestoreCommand(log, System.IO.Path.Combine(TestRoot, relativePath))
                .AddSourcesFromCurrentConfig();
        }

        public TestAsset Restore(ITestOutputHelper log, string relativePath = "", params string[] args)
        {
            var commandResult = GetRestoreCommand(log, relativePath)
                .Execute(args);

            commandResult.Should().Pass();

            return this;
        }

        public TestAsset NuGetRestore(ITestOutputHelper log, string relativePath = "", params string[] args)
        {
            var commandResult = GetNuGetRestoreCommand(log, relativePath)
                .Execute(args);

            commandResult.Should().Pass();

            return this;
        }

        private bool IsBinOrObjFolder(string directory)
        {
            var binFolder = $"{System.IO.Path.DirectorySeparatorChar}bin";
            var objFolder = $"{System.IO.Path.DirectorySeparatorChar}obj";

            directory = directory.ToLowerInvariant();
            return directory.EndsWith(binFolder)
                  || directory.EndsWith(objFolder)
                  || IsInBinOrObjFolder(directory);
        }

        private bool IsInBinOrObjFolder(string path)
        {
            var objFolderWithTrailingSlash =
              $"{System.IO.Path.DirectorySeparatorChar}obj{System.IO.Path.DirectorySeparatorChar}";
            var binFolderWithTrailingSlash =
              $"{System.IO.Path.DirectorySeparatorChar}bin{System.IO.Path.DirectorySeparatorChar}";

            path = path.ToLowerInvariant();
            return path.Contains(binFolderWithTrailingSlash)
                  || path.Contains(objFolderWithTrailingSlash);
        }
    }
}
