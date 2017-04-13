// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using static Microsoft.NET.TestFramework.Commands.MSBuildTest;
using System.Collections.Generic;

namespace Microsoft.NET.TestFramework
{
    public class TestAsset : TestDirectory
    {
        private readonly string _testAssetRoot;

        public string BuildVersion { get; }

        private List<string> _projectFiles;

        public string TestRoot => Path;

        internal TestAsset(string testDestination, string buildVersion) : base(testDestination)
        {
            BuildVersion = buildVersion;
        }

        internal TestAsset(string testAssetRoot, string testDestination, string buildVersion) : base(testDestination)
        {
            if (string.IsNullOrEmpty(testAssetRoot))
            {
                throw new ArgumentException("testAssetRoot");
            }

            _testAssetRoot = testAssetRoot;
            BuildVersion = buildVersion;
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

                    SetSdkVersion(project);

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

        public void SetSdkVersion(XDocument project)
        {
            var ns = project.Root.Name.Namespace;

            project
                .Descendants(ns + "PackageReference")
                .FirstOrDefault(pr => pr.Attribute("Include")?.Value == "Microsoft.NET.Sdk")
                ?.Element(ns + "Version")
                ?.SetValue($"[{BuildVersion}]");

        }

        public RestoreCommand GetRestoreCommand(string relativePath = "", params string[] args)
        {
            return new RestoreCommand(Stage0MSBuild, System.IO.Path.Combine(TestRoot, relativePath))
                .AddSourcesFromCurrentConfig()
                .AddSource(RepoInfo.PackagesPath);
        }

        public TestAsset Restore(string relativePath = "", params string[] args)
        {
            var commandResult = GetRestoreCommand(relativePath, args)
                .Execute(args);

            commandResult.Should().Pass();

            return this;
        }

        private bool IsBinOrObjFolder(string directory)
        {
            var binFolder = $"{System.IO.Path.DirectorySeparatorChar}bin";
            var objFolder = $"{System.IO.Path.DirectorySeparatorChar}obj";

            directory = directory.ToLower();
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

            path = path.ToLower();
            return path.Contains(binFolderWithTrailingSlash)
                  || path.Contains(objFolderWithTrailingSlash);
        }

        public static IEnumerable<Tuple<string, string>> ConflictResolutionDependencies
        {
            get
            {
                string netstandardDependenciesXml = @" 
    <group targetFramework="".NETStandard1.3"">
        <!--dependency id=""Microsoft.NETCore.Platforms"" version=""1.1.0"" /-->
        <dependency id=""Microsoft.Win32.Primitives"" version=""4.3.0"" />
        <dependency id=""System.AppContext"" version=""4.3.0"" />
        <dependency id=""System.Collections"" version=""4.3.0"" />
        <dependency id=""System.Collections.Concurrent"" version=""4.3.0"" />
        <dependency id=""System.Console"" version=""4.3.0"" />
        <dependency id=""System.Diagnostics.Debug"" version=""4.3.0"" />
        <dependency id=""System.Diagnostics.Tools"" version=""4.3.0"" />
        <dependency id=""System.Diagnostics.Tracing"" version=""4.3.0"" />
        <dependency id=""System.Globalization"" version=""4.3.0"" />
        <dependency id=""System.Globalization.Calendars"" version=""4.3.0"" />
        <dependency id=""System.IO"" version=""4.3.0"" />
        <dependency id=""System.IO.Compression"" version=""4.3.0"" />
        <dependency id=""System.IO.Compression.ZipFile"" version=""4.3.0"" />
        <dependency id=""System.IO.FileSystem"" version=""4.3.0"" />
        <dependency id=""System.IO.FileSystem.Primitives"" version=""4.3.0"" />
        <dependency id=""System.Linq"" version=""4.3.0"" />
        <dependency id=""System.Linq.Expressions"" version=""4.3.0"" />
        <dependency id=""System.Net.Http"" version=""4.3.0"" />
        <dependency id=""System.Net.Primitives"" version=""4.3.0"" />
        <dependency id=""System.Net.Sockets"" version=""4.3.0"" />
        <dependency id=""System.ObjectModel"" version=""4.3.0"" />
        <dependency id=""System.Reflection"" version=""4.3.0"" />
        <dependency id=""System.Reflection.Extensions"" version=""4.3.0"" />
        <dependency id=""System.Reflection.Primitives"" version=""4.3.0"" />
        <dependency id=""System.Resources.ResourceManager"" version=""4.3.0"" />
        <dependency id=""System.Runtime"" version=""4.3.0"" />
        <dependency id=""System.Runtime.Extensions"" version=""4.3.0"" />
        <dependency id=""System.Runtime.Handles"" version=""4.3.0"" />
        <dependency id=""System.Runtime.InteropServices"" version=""4.3.0"" />
        <dependency id=""System.Runtime.InteropServices.RuntimeInformation"" version=""4.3.0"" />
        <dependency id=""System.Runtime.Numerics"" version=""4.3.0"" />
        <dependency id=""System.Security.Cryptography.Algorithms"" version=""4.3.0"" />
        <dependency id=""System.Security.Cryptography.Encoding"" version=""4.3.0"" />
        <dependency id=""System.Security.Cryptography.Primitives"" version=""4.3.0"" />
        <dependency id=""System.Security.Cryptography.X509Certificates"" version=""4.3.0"" />
        <dependency id=""System.Text.Encoding"" version=""4.3.0"" />
        <dependency id=""System.Text.Encoding.Extensions"" version=""4.3.0"" />
        <dependency id=""System.Text.RegularExpressions"" version=""4.3.0"" />
        <dependency id=""System.Threading"" version=""4.3.0"" />
        <dependency id=""System.Threading.Tasks"" version=""4.3.0"" />
        <dependency id=""System.Threading.Timer"" version=""4.3.0"" />
        <dependency id=""System.Xml.ReaderWriter"" version=""4.3.0"" />
        <dependency id=""System.Xml.XDocument"" version=""4.3.0"" />
      </group>";

                XElement netStandardDependencies = XElement.Parse(netstandardDependenciesXml);

                foreach (var dependency in netStandardDependencies.Elements("dependency"))
                {
                    yield return Tuple.Create(dependency.Attribute("id").Value, dependency.Attribute("version").Value);
                }

                yield return Tuple.Create("System.Diagnostics.TraceSource", "4.0.0");
            }
        }

        public static string ConflictResolutionTestMethod
        {
            get
            {
                return @"
    public static void TestConflictResolution()
    {
        new System.Diagnostics.TraceSource(""ConflictTest"");
    }";
            }
        }
    }
}
