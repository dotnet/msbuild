// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.TestFramework
{
    /// <summary>
    /// A directory wrapper around the <see cref="TestProject"/> class, or any other TestAsset type.
    /// It manages the on-disk files of the test asset and provides additional functionality to edit projects.
    /// </summary>
    public class TestAsset : TestDirectory
    {
        private readonly string _testAssetRoot;

        private List<string> _projectFiles;

        public string TestRoot => Path;

        /// <summary>
        /// The hashed test name (so file paths do not become too long) of the TestAsset owning test.
        /// Contains the leaf folder name of any particular test's root folder.
        /// The hashing occurs in <see cref="TestAssetsManager"/>.
        /// </summary>
        public readonly string Name;

        public ITestOutputHelper Log { get; }

        //  The TestProject from which this asset was created, if any
        public TestProject TestProject { get; set; }

        internal TestAsset(string testDestination, string sdkVersion, ITestOutputHelper log) : base(testDestination, sdkVersion)
        {
            Log = log;
            Name = new DirectoryInfo(testDestination).Name;
        }

        internal TestAsset(string testAssetRoot, string testDestination, string sdkVersion, ITestOutputHelper log) : base(testDestination, sdkVersion)
        {
            if (string.IsNullOrEmpty(testAssetRoot))
            {
                throw new ArgumentException("testAssetRoot");
            }

            Log = log;
            Name = new DirectoryInfo(testAssetRoot).Name;
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
                
                if (System.IO.Path.GetFileName(srcFile).EndsWith("proj") || System.IO.Path.GetFileName(srcFile).EndsWith("xml"))
                {
                    _projectFiles.Add(destFile);
                }
                File.Copy(srcFile, destFile, true);
            }

            string[][] Properties = {
                new string[] { "TargetFramework", "$(CurrentTargetFramework)", ToolsetInfo.CurrentTargetFramework },
                new string[] { "CurrentTargetFramework", "$(CurrentTargetFramework)", ToolsetInfo.CurrentTargetFramework },
                new string[] { "RuntimeIdentifier", "$(LatestWinRuntimeIdentifier)", ToolsetInfo.LatestWinRuntimeIdentifier },
                new string[] { "RuntimeIdentifier", "$(LatestLinuxRuntimeIdentifier)", ToolsetInfo.LatestLinuxRuntimeIdentifier },
                new string[] { "RuntimeIdentifier", "$(LatestMacRuntimeIdentifier)", ToolsetInfo.LatestMacRuntimeIdentifier },
                new string[] { "RuntimeIdentifier", "$(LatestRuntimeIdentifiers)", ToolsetInfo.LatestRuntimeIdentifiers } };

            foreach (string[] property in Properties)
            {
                this.UpdateProjProperty(property[0], property[1], property[2]);
            }

            this.ReplaceTheNewtonsoftJsonPackageVersionVariable();

            return this;
        }

        public TestAsset UpdateProjProperty(string propertyName, string variableName, string targetValue)
        {
            return WithProjectChanges(
            p =>
            {
                var ns = p.Root.Name.Namespace;
                var getNode = p.Root.Elements(ns + "PropertyGroup").Elements(ns + propertyName).FirstOrDefault();
                getNode ??= p.Root.Elements(ns + "PropertyGroup").Elements(ns + $"{propertyName}s").FirstOrDefault();
                getNode?.SetValue(getNode?.Value.Replace(variableName, targetValue));
            });
        }

        public TestAsset ReplaceTheNewtonsoftJsonPackageVersionVariable()
        {
            string[] PropertyNames = new[] { "PackageReference", "Package" };
            string targetName = "NewtonsoftJsonPackageVersion";

            return WithProjectChanges(project =>
            {
                var ns = project.Root.Name.Namespace;
                foreach (var PropertyName in PropertyNames)
                {
                    var packageReferencesToUpdate =
                        project.Root.Descendants(ns + PropertyName)
                            .Where(p => p.Attribute("Version") != null && p.Attribute("Version").Value.Equals($"$({targetName})", StringComparison.OrdinalIgnoreCase));
                    foreach (var packageReference in packageReferencesToUpdate)
                    {
                        packageReference.Attribute("Version").Value = ToolsetInfo.GetNewtonsoftJsonPackageVersion();
                    }
                }
            });
        }

        public TestAsset WithTargetFramework(string targetFramework, string projectName = null)
        {
            if (targetFramework == null)
            {
                return this;
            }
            return WithProjectChanges(
            p =>
            {
                var ns = p.Root.Name.Namespace;
                p.Root.Elements(ns + "PropertyGroup").Elements(ns + "TargetFramework").Single().SetValue(targetFramework);
            },
            projectName);
        }

        public TestAsset WithTargetFrameworks(string targetFrameworks, string projectName = null)
        {
            if (targetFrameworks == null)
            {
                return this;
            }
            return WithProjectChanges(
            p =>
            {
                var ns = p.Root.Name.Namespace;
                var propertyGroup = p.Root.Elements(ns + "PropertyGroup").First();
                propertyGroup.Elements(ns + "TargetFramework").SingleOrDefault()?.Remove();
                propertyGroup.Elements(ns + "TargetFrameworks").SingleOrDefault()?.Remove();
                propertyGroup.Add(new XElement(ns + "TargetFrameworks", targetFrameworks));
            },
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

        private TestAsset WithProjectChanges(Action<XDocument> actionOnProject, string projectName = null)
        {
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

        public TestAsset Restore(ITestOutputHelper log, string relativePath = "", params string[] args)
        {
            var commandResult = GetRestoreCommand(log, relativePath)
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
