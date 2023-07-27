// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using FluentAssertions;
using Microsoft.Build.Framework;
using Microsoft.NET.TestFramework;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenThatWeWantToGetDependenciesViaDesignTimeBuild : SdkTest
    {
        public GivenThatWeWantToGetDependenciesViaDesignTimeBuild(ITestOutputHelper log) : base(log)
        {
        }

        [WindowsOnlyFact]
        public void ItShouldIgnoreAllDependenciesWithTypeNotEqualToPackageOrUnresolved()
        {
            var testRoot = _testAssetsManager.CreateTestDirectory().Path;
            Log.WriteLine("Test root: " + testRoot);

            string projectAssetsJsonPath = Path.Combine(testRoot, "project.assets.json");
            string projectCacheAssetsJsonPath = Path.Combine(testRoot, "projectassets.cache");
            // project.assets.json
            var assetsContent = CreateBasicProjectAssetsFile(testRoot, package2Type: "unresolved", package3Type: "unknown");

            File.WriteAllText(projectAssetsJsonPath, assetsContent);
            var task = InitializeTask(testRoot, out _);
            task.ProjectAssetsFile = projectAssetsJsonPath;
            task.ProjectAssetsCacheFile = projectCacheAssetsJsonPath;
            task.TargetFramework = "net6.0";

            task.Execute();

            Assert.Equal(2, task.PackageDependenciesDesignTime.Count());

            // Verify only 
            // top.package1 is type 'package'
            // top.package2 is type 'unresolved'
            // 
            // top.package3 is type 'unknown'. Should not appear in the list
            var item1 = task.PackageDependenciesDesignTime[0];
            Assert.Equal("top.package1/1.0.0", item1.ItemSpec);

            var item2 = task.PackageDependenciesDesignTime[1];
            Assert.Equal("top.package2/1.0.0", item2.ItemSpec);
        }

        [WindowsOnlyFact]
        public void ItShouldIdentifyDefaultImplicitPackages()
        {
            var testRoot = _testAssetsManager.CreateTestDirectory().Path;
            Log.WriteLine("Test root: " + testRoot);

            string projectAssetsJsonPath = Path.Combine(testRoot, "project.assets.json");
            string projectCacheAssetsJsonPath = Path.Combine(testRoot, "projectassets.cache");
            // project.assets.json
            File.WriteAllText(projectAssetsJsonPath, CreateBasicProjectAssetsFile(testRoot));
            var task = InitializeTask(testRoot, out _);
            task.ProjectAssetsFile = projectAssetsJsonPath;
            task.ProjectAssetsCacheFile = projectCacheAssetsJsonPath;
            task.TargetFramework = "net6.0";
            // Set implicit packages
            task.DefaultImplicitPackages = "top.package2;top.package3";

            task.Execute();

            Assert.Equal(3, task.PackageDependenciesDesignTime.Count());

            // Verify implicit packages
            var item1 = task.PackageDependenciesDesignTime[0];
            Assert.Equal("top.package1/1.0.0", item1.ItemSpec);
            Assert.Equal("False", item1.GetMetadata(MetadataKeys.IsImplicitlyDefined));

            var item2 = task.PackageDependenciesDesignTime[1];
            Assert.Equal("top.package2/1.0.0", item2.ItemSpec);
            Assert.Equal("True", item2.GetMetadata(MetadataKeys.IsImplicitlyDefined));

            var item3 = task.PackageDependenciesDesignTime[2];
            Assert.Equal("top.package3/1.0.0", item3.ItemSpec);
            Assert.Equal("True", item3.GetMetadata(MetadataKeys.IsImplicitlyDefined));
        }

        [WindowsOnlyFact]
        public void ItShouldOnlyReturnPackagesInTheSpecifiedTarget()
        {
            var testRoot = _testAssetsManager.CreateTestDirectory().Path;
            Log.WriteLine("Test root: " + testRoot);

            string projectAssetsJsonPath = Path.Combine(testRoot, "project.assets.json");
            string projectCacheAssetsJsonPath = Path.Combine(testRoot, "projectassets.cache");
            // project.assets.json
            var assetsContent =
"""
{
  "version" : 3,
  "targets" : {
    "net6.0" :{
      "top.package1/1.0.0": {
        "type": "package",
        "dependencies": {
          "dependent.package1": "1.0.0",
          "dependent.package2": "1.0.0",
        },
        "compile": {
          "lib/netstandard2.1/top.package1.dll": {
            "related": ".xml"
          }
        },
        "runtime": {
          "lib/netstandard2.1/top.package1.dll": {
            "related": ".xml"
          }
        }
      },
      "dependent.package1/1.0.0": {
        "type": "package",
        "dependencies": {
          "dependent.package3": "1.0.0"
        },
        "compile": {
          "lib/netcoreapp3.1/_._": {
            "related": ".xml"
          }
        },
        "runtime": {
          "lib/netcoreapp3.1/dependent.package1.dll": {
            "related": ".xml"
          }
        }
      },
      "dependent.package2/1.0.0": {
        "type": "package",
        "compile": {
          "lib/netstandard2.0/dependent.package2.dll": {
            "related": ".xml"
          }
        },
        "runtime": {
          "lib/netstandard2.0/dependent.package2.dll": {
            "related": ".xml"
          }
        }
      },
      "dependent.package3/1.0.0": {
          "type": "package",
          "compile": {
          "lib/netstandard2.1/dependent.package3.dll": {
              "related": ".xml"
          }
        },
        "runtime": {
          "lib/netstandard2.1/dependent.package3.dll": {
              "related": ".xml"
          }
        }
      }
    },
    "net7.0" : {
      "top.package2/1.0.0" : {
        "type" : "package",
        "compile" : {
            "lib/netstandard2.0/top.package2.dll" : {
                "related" : ".pdb;.xml"
            }
        },
        "runtime" : {
            "lib/netstandard2.0/top.package2.dll" : {
                "related": ".pdb;.xml"
            }
        }
      },
      "top.package3/1.0.0" : {
          "type" : "package",
          "dependencies": {
              "dependent.package1" : "1.0.1"
          },
          "compile" : {
              "lib/netstandard2.0/top.package3.dll" : {
                  "related" : ".pdb;.xml"
              }
          },
          "runtime" : {
              "lib/netstandard2.0/top.package3.dll" : {
                  "related" : ".pdb;.xml"
               }
          }
      },
      "dependent.package1/1.0.0": {
        "type": "package",
        "dependencies": {
          "dependent.package3": "1.0.0"
        },
        "compile": {
          "lib/netcoreapp3.1/_._": {
            "related": ".xml"
          }
        },
        "runtime": {
          "lib/netcoreapp3.1/dependent.package1.dll": {
            "related": ".xml"
          }
        }
      },
      "dependent.package3/1.0.0": {
          "type": "package",
          "compile": {
          "lib/netstandard2.1/dependent.package3.dll": {
              "related": ".xml"
          }
        },
        "runtime": {
          "lib/netstandard2.1/dependent.package3.dll": {
              "related": ".xml"
          }
        }
      }
    }
  },
  "libraries" : {
    "dependent.package1/1.0.0" : {
        "sha512" : "xyz",
        "type" : "package",
        "path" : "dependent.package1/1.0.0",
        "files" : [
          "lib/net461/dependent.package1.dll",
          "lib/net461/dependent.package1.xml"
        ]
    },
    "dependent.package2/1.0.0" : {
        "sha512" : "xyz",
        "type" : "package",
        "path" : "dependent.package2/1.0.0",
        "files" : [
          ".nupkg.metadata"
        ]
    },
    "dependent.package3/1.0.0" : {
        "sha512" : "xyz",
        "type" : "package",
        "path" : "dependent.package3/1.0.0",
        "files" : [
          "lib/net472/dependent.package3.dll",
          "lib/net472/dependent.package3.xml"
        ]
    },
    "top.package1/1.0.0" : {
        "sha512" : "xyz",
        "type" : "package",
        "path" : "top.package1/1.0.0",
        "files" : [
            "lib/net20/top.package1.dll",
            "lib/net20/top.package1.pdb",
            "lib/net20/top.package1.xml"
        ]
    },
    "top.package2/1.0.0" : {
        "sha512" : "abc",
        "type" : "package",
        "path" : "top.package2/1.0.0",
        "files" : [
            "lib/net45/top.package2.dll",
            "lib/net45/top.package2.pdb",
            "lib/net45/top.package2.xml"
        ]
    },
    "top.package3/1.0.0" : {
        "sha512" : "abc",
        "type" : "package",
        "path" : "top.package3/1.0.0",
        "files" : [
           ".nupkg.metadata",
        ]
    }
  },
  "projectFileDependencyGroups": {
    "net6.0": [
      "top.package1 >= 1.0.0"
    ],
    "net7.0": [
      "top.package2 >= 1.0.0",
      "top.package3 >= 1.0.0"
    ]
  },
  "packageFolders": {
    "C:\\.nuget\\packages\\" : {}
  },
  "project" : {
      "version" : "1.0.0",
      "restore": {
          "projectUniqueName": "C:\\projDir1\\projDir2\\proj.csproj",
          "projectName": "proj",
          "projectPath": "C:\\projDir1\\projDir2\\proj.csproj",
          "packagesPath": "C:\\.nuget\\packages\\",
          "outputPath": "C:\\projDir1\\projDir2\\obj\\",
          "projectStyle": "PackageReference",
          "fallbackFolders": [
            "C:\\fallbackDir\\NuGetPackages"
          ],
          "configFilePaths": [
            "C:\\configDir\\NuGet.Config",
            "C:\\configDir2\\Microsoft.VisualStudio.FallbackLocation.config",
            "C:\\configDir3\\Microsoft.VisualStudio.Offline.config"
          ],
          "originalTargetFrameworks": [
            "net6.0"
          ],
          "frameworks": {
            "net6.0": {
              "targetAlias": "net6.0",
              "projectReferences": {}
            }
          },
          "warningProperties": {
            "warnAsError": [
              "NU1605"
            ]
      }
    },
      "frameworks" : {
          "net6.0": {
              "targetAlias" : "net6.0",
              "dependencies" : {
                  "top.package1" : {
                      "target" : "Package",
                      "version" : "[1.0.0, )"
                   }
              }
          }
      }
  }
}
""";
            assetsContent = assetsContent.Replace(@"C:\\.nuget", $@"{testRoot.Replace("\\", "\\\\")}\\.nuget"); ;

            File.WriteAllText(projectAssetsJsonPath, assetsContent);
            var task = InitializeTask(testRoot, out _);
            task.ProjectAssetsFile = projectAssetsJsonPath;
            task.ProjectAssetsCacheFile = projectCacheAssetsJsonPath;
            // Set target to verify
            task.TargetFramework = "net6.0";
            task.Execute();

            Assert.Single(task.PackageDependenciesDesignTime);

            // Verify top packages in target
            var item1 = task.PackageDependenciesDesignTime[0];
            Assert.Equal("top.package1/1.0.0", item1.ItemSpec);
        }

        [WindowsOnlyFact]
        public void ItShouldOnlyReturnTopLevelPackages()
        {
            var testRoot = _testAssetsManager.CreateTestDirectory().Path;
            Log.WriteLine("Test root: " + testRoot);

            string projectAssetsJsonPath = Path.Combine(testRoot, "project.assets.json");
            string projectCacheAssetsJsonPath = Path.Combine(testRoot, "projectassets.cache");
            // project.assets.json
            File.WriteAllText(projectAssetsJsonPath, CreateBasicProjectAssetsFile(testRoot));
            var task = InitializeTask(testRoot, out _);
            task.ProjectAssetsFile = projectAssetsJsonPath;
            task.ProjectAssetsCacheFile = projectCacheAssetsJsonPath;
            task.TargetFramework = "net6.0";
            task.Execute();

            // Verify all top packages are listed here
            Assert.Equal(3, task.PackageDependenciesDesignTime.Count());

            var item1 = task.PackageDependenciesDesignTime[0];
            Assert.Equal("top.package1/1.0.0", item1.ItemSpec);

            var item2 = task.PackageDependenciesDesignTime[1];
            Assert.Equal("top.package2/1.0.0", item2.ItemSpec);

            var item3 = task.PackageDependenciesDesignTime[2];
            Assert.Equal("top.package3/1.0.0", item3.ItemSpec);
        }

        private string CreateBasicProjectAssetsFile(string testRoot, string package2Type = "package", string package3Type = "package")
        {
            var json =
"""
{
  "version" : 3,
  "targets" : {
    "net6.0" : {
      "top.package1/1.0.0": {
        "type": "package",
        "dependencies": {
          "dependent.package1": "1.0.0",
          "dependent.package2": "1.0.0",
        },
        "compile": {
          "lib/netstandard2.1/top.package1.dll": {
            "related": ".xml"
          }
        },
        "runtime": {
          "lib/netstandard2.1/top.package1.dll": {
            "related": ".xml"
          }
        }
      },
      "top.package2/1.0.0" : {
        "type" : "PACKAGE2_TYPE",
        "compile" : {
            "lib/netstandard2.0/top.package2.dll" : {
                "related" : ".pdb;.xml"
            }
        },
        "runtime" : {
            "lib/netstandard2.0/top.package2.dll" : {
                "related": ".pdb;.xml"
            }
        }
      },
      "top.package3/1.0.0" : {
          "type" : "PACKAGE3_TYPE",
          "dependencies": {
              "dependent.package1" : "1.0.1"
          },
          "compile" : {
              "lib/netstandard2.0/top.package3.dll" : {
                  "related" : ".pdb;.xml"
              }
          },
          "runtime" : {
              "lib/netstandard2.0/top.package3.dll" : {
                  "related" : ".pdb;.xml"
               }
          }
      },
      "dependent.package1/1.0.0": {
        "type": "package",
        "dependencies": {
          "dependent.package3": "1.0.0"
        },
        "compile": {
          "lib/netcoreapp3.1/_._": {
            "related": ".xml"
          }
        },
        "runtime": {
          "lib/netcoreapp3.1/dependent.package1.dll": {
            "related": ".xml"
          }
        }
      },
      "dependent.package2/1.0.0": {
        "type": "package",
        "compile": {
          "lib/netstandard2.0/dependent.package2.dll": {
            "related": ".xml"
          }
        },
        "runtime": {
          "lib/netstandard2.0/dependent.package2.dll": {
            "related": ".xml"
          }
        }
      },
      "dependent.package3/1.0.0": {
          "type": "package",
          "compile": {
          "lib/netstandard2.1/dependent.package3.dll": {
              "related": ".xml"
          }
        },
        "runtime": {
          "lib/netstandard2.1/dependent.package3.dll": {
              "related": ".xml"
          }
        }
      }
    }
  },
  "libraries" : {
    "dependent.package1/1.0.0" : {
        "sha512" : "xyz",
        "type" : "package",
        "path" : "dependent.package1/1.0.0",
        "files" : [
          "lib/net461/dependent.package1.dll",
          "lib/net461/dependent.package1.xml"
        ]
    },
    "dependent.package2/1.0.0" : {
        "sha512" : "xyz",
        "type" : "package",
        "path" : "dependent.package2/1.0.0",
        "files" : [
          ".nupkg.metadata"
        ]
    },
    "dependent.package3/1.0.0" : {
        "sha512" : "xyz",
        "type" : "package",
        "path" : "dependent.package3/1.0.0",
        "files" : [
          "lib/net472/dependent.package3.dll",
          "lib/net472/dependent.package3.xml"
        ]
    },
    "top.package1/1.0.0" : {
        "sha512" : "xyz",
        "type" : "package",
        "path" : "top.package1/1.0.0",
        "files" : [
            "lib/net20/top.package1.dll",
            "lib/net20/top.package1.pdb",
            "lib/net20/top.package1.xml"
        ]
    },
    "top.package2/1.0.0" : {
        "sha512" : "abc",
        "type" : "PACKAGE2_TYPE",
        "path" : "top.package2/1.0.0",
        "files" : [
            "lib/net45/top.package2.dll",
            "lib/net45/top.package2.pdb",
            "lib/net45/top.package2.xml"
        ]
    },
    "top.package3/1.0.0" : {
        "sha512" : "abc",
        "type" : "PACKAGE3_TYPE",
        "path" : "top.package3/1.0.0",
        "files" : [
           ".nupkg.metadata",
        ]
    }
  },
  "projectFileDependencyGroups": {
    "net6.0": [
      "top.package1 >= 1.0.0",
      "top.package2 >= 1.0.0",
      "top.package3 >= 1.0.0"
    ]
  },
  "packageFolders": {
    "C:\\.nuget\\packages\\" : {}
  },
  "project" : {
      "version" : "1.0.0",
      "restore": {
          "projectUniqueName": "C:\\projDir1\\projDir2\\proj.csproj",
          "projectName": "proj",
          "projectPath": "C:\\projDir1\\projDir2\\proj.csproj",
          "packagesPath": "C:\\.nuget\\packages\\",
          "outputPath": "C:\\projDir1\\projDir2\\obj\\",
          "projectStyle": "PackageReference",
          "fallbackFolders": [
            "C:\\fallbackDir\\NuGetPackages"
          ],
          "configFilePaths": [
            "C:\\configDir\\NuGet.Config",
            "C:\\configDir2\\Microsoft.VisualStudio.FallbackLocation.config",
            "C:\\configDir3\\Microsoft.VisualStudio.Offline.config"
          ],
          "originalTargetFrameworks": [
            "net6.0"
          ],
          "frameworks": {
            "net6.0": {
              "targetAlias": "net6.0",
              "projectReferences": {}
            }
          },
          "warningProperties": {
            "warnAsError": [
              "NU1605"
            ]
      }
    },
      "frameworks" : {
          "net6.0": {
              "targetAlias" : "net6.0",
              "dependencies" : {
                  "top.package1" : {
                      "target" : "Package",
                      "version" : "[1.0.0, )"
                   }
              }
          }
      }
  }
}
""";
            return json.Replace("PACKAGE2_TYPE", package2Type)
                       .Replace("PACKAGE3_TYPE", package3Type)
                       .Replace(@"C:\\.nuget", $@"{testRoot.Replace("\\", "\\\\")}\\.nuget");
        }
        private ResolvePackageAssets InitializeTask(string testRoot, out IEnumerable<PropertyInfo> inputProperties)
        {
            inputProperties = typeof(ResolvePackageAssets)
                .GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public)
                .Where(p => !p.IsDefined(typeof(OutputAttribute)) &&
                            p.Name != nameof(ResolvePackageAssets.DesignTimeBuild))
                .OrderBy(p => p.Name, StringComparer.Ordinal);

            var requiredProperties = inputProperties
                .Where(p => p.IsDefined(typeof(RequiredAttribute)));

            var task = new ResolvePackageAssets();
            // Initialize all required properties as a genuine task invocation would. We do this
            // because HashSettings need not defend against required parameters being null.
            foreach (var property in requiredProperties)
            {
                property.PropertyType.Should().Be(
                    typeof(string),
                    because: $"this test hasn't been updated to handle non-string required task parameters like {property.Name}");

                property.SetValue(task, "_");
            }

            task.BuildEngine = new MockBuildEngine();

            CreateFolders(testRoot);

            return task;
        }

        private void CreateFolders(string testRoot)
        {
            Directory.CreateDirectory($@"{testRoot}\.nuget\packages\top.package1\1.0.0\");
            Directory.CreateDirectory($@"{testRoot}\.nuget\packages\top.package2\1.0.0\");
            Directory.CreateDirectory($@"{testRoot}\.nuget\packages\top.package3\1.0.0\");
            Directory.CreateDirectory($@"{testRoot}\.nuget\packages\dependent.package1\1.0.0\");
            Directory.CreateDirectory($@"{testRoot}\.nuget\packages\dependent.package2\1.0.0\");
            Directory.CreateDirectory($@"{testRoot}\.nuget\packages\dependent.package3\1.0.0\");
            using (File.Create($@"{testRoot}\.nuget\packages\top.package1\1.0.0\.nupkg.metadata")) { }
            using (File.Create($@"{testRoot}\.nuget\packages\top.package1\1.0.0\top.package1.1.0.0.nupkg.sha512")) { }
            using (File.Create($@"{testRoot}\.nuget\packages\top.package2\1.0.0\.nupkg.metadata")) { }
            using (File.Create($@"{testRoot}\.nuget\packages\top.package2\1.0.0\top.package2.1.0.0.nupkg.sha512")) { }
            using (File.Create($@"{testRoot}\.nuget\packages\top.package3\1.0.0\.nupkg.metadata")) { }
            using (File.Create($@"{testRoot}\.nuget\packages\top.package3\1.0.0\top.package3.1.0.0.nupkg.sha512")) { }
            using (File.Create($@"{testRoot}\.nuget\packages\dependent.package1\1.0.0\.nupkg.metadata")) { }
            using (File.Create($@"{testRoot}\.nuget\packages\dependent.package1\1.0.0\dependent.package1.10.0.nupkg.sha512")) { }
            using (File.Create($@"{testRoot}\.nuget\packages\dependent.package2\1.0.0\.nupkg.metadata")) { }
            using (File.Create($@"{testRoot}\.nuget\packages\dependent.package2\1.0.0\dependent.package2.1.0.0.nupkg.sha512")) { }
            using (File.Create($@"{testRoot}\.nuget\packages\dependent.package3\1.0.0\.nupkg.metadata")) { }
            using (File.Create($@"{testRoot}\.nuget\packages\dependent.package3\1.0.0\dependent.package3.1.0.0.nupkg.sha512")) { }

        }
    }
}

