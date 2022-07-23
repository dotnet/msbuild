// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.Build.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;
using static Microsoft.NET.Build.Tasks.ResolvePackageAssets;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAResolvePackageAssetsTask
    {
        [Fact]
        public void ItHashesAllParameters()
        {
            IEnumerable<PropertyInfo> inputProperties;

            var task = InitializeTask(out inputProperties);

            byte[] oldHash;
            try
            {
                 oldHash = task.HashSettings();
            }
            catch (ArgumentNullException)
            {
                Assert.True(
                    false, 
                    "HashSettings is likely not correctly handling null value of one or more optional task parameters");

                throw; // unreachable
            }

            foreach (var property in inputProperties)
            {
                switch (property.PropertyType)
                {
                    case var t when t == typeof(bool):
                        property.SetValue(task, true);
                        break;

                    case var t when t == typeof(string):
                        property.SetValue(task, property.Name);
                        break;

                    case var t when t == typeof(ITaskItem[]):
                        property.SetValue(task, new[] { new MockTaskItem() { ItemSpec = property.Name } });
                        break;

                    default:
                        Assert.True(false, $"{property.Name} is not a bool or string or ITaskItem[]. Update the test code to handle that.");
                        throw null; // unreachable
                }

                byte[] newHash = task.HashSettings();
                newHash.Should().NotBeEquivalentTo(
                    oldHash, 
                    because: $"{property.Name} should be included in hash.");

                oldHash = newHash;
            }
        }

        [Fact]
        public void ItDoesNotHashDesignTimeBuild()
        {
            var task = InitializeTask(out _);

            task.DesignTimeBuild = false;

            byte[] oldHash = task.HashSettings();

            task.DesignTimeBuild = true;

            byte[] newHash = task.HashSettings();

            newHash.Should().BeEquivalentTo(oldHash,
                because: $"{nameof(task.DesignTimeBuild)} should not be included in hash.");
        }

        [Fact]
        public void It_does_not_error_on_duplicate_package_names()
        {
            string projectAssetsJsonPath = Path.GetTempFileName();
            var assetsContent = @"{
  `version`: 3,
  `targets`: {
    `net5.0`: {
      `Humanizer.Core/2.8.25`: {
        `type`: `package`
      },
      `Humanizer.Core/2.8.26`: {
        `type`: `package`
      }
    }
  },
  `project`: {
    `version`: `1.0.0`,
    `frameworks`: {
      `net5.0`: {
        `targetAlias`: `net5.0`
      }
    }
  }
}".Replace('`', '"');
            File.WriteAllText(projectAssetsJsonPath, assetsContent);

            var task = InitializeTask(out _);
            task.ProjectAssetsFile = projectAssetsJsonPath;
            task.TargetFramework = "net5.0";
            new CacheWriter(task); // Should not error
        }

        [Fact]
        public void It_warns_on_invalid_culture_codes_of_resources()
        {
            string projectAssetsJsonPath = Path.GetTempFileName();
            var assetsContent = @"
{
  `version`: 3,
  `targets`: {
    `net7.0`: {
      `JavaScriptEngineSwitcher.Core/3.3.0`: {
        `type`: `package`,
        `compile`: {
          `lib/netstandard2.0/JavaScriptEngineSwitcher.Core.dll`: {}
        },
        `runtime`: {
          `lib/netstandard2.0/JavaScriptEngineSwitcher.Core.dll`: {}
        },
        `resource`: {
          `lib/netstandard2.0/ru-ru/JavaScriptEngineSwitcher.Core.resources.dll`: {
            `locale`: `what is this even`
          }
        }
      }
    }
  }
}".Replace("`", "\"");
            File.WriteAllText(projectAssetsJsonPath, assetsContent);
            var task = InitializeTask(out _);
            task.ProjectAssetsFile = projectAssetsJsonPath;
            task.TargetFramework = "net7.0";
            var writer = new CacheWriter(task);
            writer.WriteToCacheFile();
            var messages = (task.BuildEngine as MockBuildEngine).Warnings;
            var invalidContextMessages = messages.Where(msg => msg.Code == "NETSDK1188");
            invalidContextMessages.Should().HaveCount(1);
        }
        
        [Fact]
        public void It_warns_on_incorrectly_cased_culture_codes_of_resources()
        {
            string projectAssetsJsonPath = Path.GetTempFileName();
            var assetsContent = @"
{
  `version`: 3,
  `targets`: {
    `net7.0`: {
      `JavaScriptEngineSwitcher.Core/3.3.0`: {
        `type`: `package`,
        `compile`: {
          `lib/netstandard2.0/JavaScriptEngineSwitcher.Core.dll`: {}
        },
        `runtime`: {
          `lib/netstandard2.0/JavaScriptEngineSwitcher.Core.dll`: {}
        },
        `resource`: {
          `lib/netstandard2.0/ru-ru/JavaScriptEngineSwitcher.Core.resources.dll`: {
            `locale`: `ru-ru`
          }
        }
      }
    }
  }
}".Replace("`", "\"");
            File.WriteAllText(projectAssetsJsonPath, assetsContent);
            var task = InitializeTask(out _);
            task.ProjectAssetsFile = projectAssetsJsonPath;
            task.TargetFramework = "net7.0";
            var writer = new CacheWriter(task);
            writer.WriteToCacheFile();
            var messages = (task.BuildEngine as MockBuildEngine).Warnings;
            var invalidContextMessages = messages.Where(msg => msg.Code == "NETSDK1187");
            invalidContextMessages.Should().HaveCount(1);
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

            return task;
        }
    }
}

