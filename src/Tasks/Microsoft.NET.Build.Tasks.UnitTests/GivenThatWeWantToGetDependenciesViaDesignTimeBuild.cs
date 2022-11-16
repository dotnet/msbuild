// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Microsoft.Build.Framework;
using Xunit;
using static Microsoft.NET.Build.Tasks.ResolvePackageAssets;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenThatWeWantToGetDependenciesViaDesignTimeBuild
    {
        [Fact]
        public void ItShouldIgnoreAllDependenciesWithTypeNotEqualToPackageOrUnresolved()
        {
            string projectAssetsJsonPath = Path.GetTempFileName();
            string projectCacheAssetsJsonPath = Path.GetTempFileName();
            // project.assets.json
            var assetsContent =
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
        "type" : "unresolved",
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
          "type" : "unknown",
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
        "type" : "unresolved",
        "path" : "top.package2/1.0.0",
        "files" : [
            "lib/net45/top.package2.dll",
            "lib/net45/top.package2.pdb",
            "lib/net45/top.package2.xml"
        ]
    },
    "top.package3/1.0.0" : {
        "sha512" : "abc",
        "type" : "unknown",
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
            File.WriteAllText(projectAssetsJsonPath, assetsContent);
            var task = InitializeTask(out _);
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

        [Fact]
        public void ItShouldIdentifyDefaultImplicitPackages()
        {
            string projectAssetsJsonPath = Path.GetTempFileName();
            string projectCacheAssetsJsonPath = Path.GetTempFileName();
            // project.assets.json
            File.WriteAllText(projectAssetsJsonPath, CreateBasicProjectAssetsFile());
            var task = InitializeTask(out _);
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

        [Fact]
        public void ItShouldOnlyReturnPackagesInTheSpecifiedTarget()
        {
            string projectAssetsJsonPath = Path.GetTempFileName();
            string projectCacheAssetsJsonPath = Path.GetTempFileName();
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
            File.WriteAllText(projectAssetsJsonPath, assetsContent);
            var task = InitializeTask(out _);
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

        [Fact]
        public void ItShouldOnlyReturnTopLevelPackages()
        {
            string projectAssetsJsonPath = Path.GetTempFileName();
            string projectCacheAssetsJsonPath = Path.GetTempFileName();
            // project.assets.json
            File.WriteAllText(projectAssetsJsonPath, CreateBasicProjectAssetsFile());
            var task = InitializeTask(out _);
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

        private string CreateBasicProjectAssetsFile()
        {
            return
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
        }
        private ResolvePackageAssets InitializeTask(out IEnumerable<PropertyInfo> inputProperties)
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

            CreateFolders();

            return task;
        }

        private void CreateFolders()
        {
            Directory.CreateDirectory("C:\\.nuget\\packages\\top.package1\\1.0.0\\");
            Directory.CreateDirectory("C:\\.nuget\\packages\\top.package2\\1.0.0\\");
            Directory.CreateDirectory("C:\\.nuget\\packages\\top.package3\\1.0.0\\");
            Directory.CreateDirectory("C:\\.nuget\\packages\\dependent.package1\\1.0.0\\");
            Directory.CreateDirectory("C:\\.nuget\\packages\\dependent.package2\\1.0.0");
            Directory.CreateDirectory("C:\\.nuget\\packages\\dependent.package3\\1.0.0\\");
            using (File.Create("C:\\.nuget\\packages\\top.package1\\1.0.0\\.nupkg.metadata")) { }
            using (File.Create("C:\\.nuget\\packages\\top.package1\\1.0.0\\top.package1.1.0.0.nupkg.sha512")) { }
            using (File.Create("C:\\.nuget\\packages\\top.package2\\1.0.0\\.nupkg.metadata")) { }
            using (File.Create("C:\\.nuget\\packages\\top.package2\\1.0.0\\top.package2.1.0.0.nupkg.sha512")) { }
            using (File.Create("C:\\.nuget\\packages\\top.package3\\1.0.0\\.nupkg.metadata")) { }
            using (File.Create("C:\\.nuget\\packages\\top.package3\\1.0.0\\top.package3.1.0.0.nupkg.sha512")) { }
            using (File.Create("C:\\.nuget\\packages\\dependent.package1\\1.0.0\\.nupkg.metadata")) { }
            using (File.Create("C:\\.nuget\\packages\\dependent.package1\\1.0.0\\dependent.package1.10.0.nupkg.sha512")) { }
            using (File.Create("C:\\.nuget\\packages\\dependent.package2\\1.0.0\\.nupkg.metadata")) { }
            using (File.Create("C:\\.nuget\\packages\\dependent.package2\\1.0.0\\dependent.package2.1.0.0.nupkg.sha512")) { }
            using (File.Create("C:\\.nuget\\packages\\dependent.package3\\1.0.0\\.nupkg.metadata")) { }
            using (File.Create("C:\\.nuget\\packages\\dependent.package3\\1.0.0\\dependent.package3.1.0.0.nupkg.sha512")) { }

        }
    }
}

