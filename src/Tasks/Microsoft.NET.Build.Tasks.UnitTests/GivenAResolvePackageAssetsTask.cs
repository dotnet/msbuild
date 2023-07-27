// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Build.Framework;
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

        private static string AssetsFileWithInvalidLocale(string tfm, string locale) => @"
{
  `version`: 3,
  `targets`: {
    `{tfm}`: {
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
            `locale`: `{locale}`
          }
        }
      }
    }
  },
  `project`: {
    `version`: `1.0.0`,
    `frameworks`: {
      `{tfm}`: {
        `targetAlias`: `{tfm}`
      }
    }
  }
}".Replace("`", "\"").Replace("{tfm}", tfm).Replace("{locale}", locale);

        [InlineData("net7.0", true)]
        [InlineData("net6.0", false)]
        [Theory]
        public void It_warns_on_invalid_culture_codes_of_resources(string tfm, bool shouldHaveWarnings)
        {
            string projectAssetsJsonPath = Path.GetTempFileName();
            var assetsContent = AssetsFileWithInvalidLocale(tfm, "what is this even");
            File.WriteAllText(projectAssetsJsonPath, assetsContent);
            var task = InitializeTask(out _);
            task.ProjectAssetsFile = projectAssetsJsonPath;
            task.TargetFramework = tfm;
            var writer = new CacheWriter(task, new MockPackageResolver());
            writer.WriteToMemoryStream();
            var engine = task.BuildEngine as MockBuildEngine;

            var invalidContextWarnings = engine.Warnings.Where(msg => msg.Code == "NETSDK1188");
            invalidContextWarnings.Should().HaveCount(shouldHaveWarnings ? 1 : 0);

            var invalidContextMessages = engine.Messages.Where(msg => msg.Code == "NETSDK1188");
            invalidContextMessages.Should().HaveCount(shouldHaveWarnings ? 0 : 1);

        }

        [InlineData("net7.0", true)]
        [InlineData("net6.0", false)]
        [Theory]
        public void It_warns_on_incorrectly_cased_culture_codes_of_resources(string tfm, bool shouldHaveWarnings)
        {
            string projectAssetsJsonPath = Path.GetTempFileName();
            var assetsContent = AssetsFileWithInvalidLocale(tfm, "ru-ru");
            File.WriteAllText(projectAssetsJsonPath, assetsContent);
            var task = InitializeTask(out _);
            task.ProjectAssetsFile = projectAssetsJsonPath;
            task.TargetFramework = tfm;
            var writer = new CacheWriter(task, new MockPackageResolver());
            writer.WriteToMemoryStream();
            var engine = task.BuildEngine as MockBuildEngine;

            var invalidContextWarnings = engine.Warnings.Where(msg => msg.Code == "NETSDK1187");
            invalidContextWarnings.Should().HaveCount(shouldHaveWarnings ? 1 : 0);

            var invalidContextMessages = engine.Messages.Where(msg => msg.Code == "NETSDK1187");
            invalidContextMessages.Should().HaveCount(shouldHaveWarnings ? 0 : 1);
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

