// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenResolvedSDKProjectItemsAndImplicitPackages
    {
        [Fact]
        public void ItShouldCombineSdkReferencesWithImplicitPackageReferences()
        {
            // Arrange 
            var sdkReference1 = new MockTaskItem(
                itemSpec: "SdkReference1",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.Version, "2.0.1" }
                });

            var sdkReference2 = new MockTaskItem(
                itemSpec: "SdkReference2",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.Version, "1.0.1" }
                });

            var packageReference1 = new MockTaskItem(
                itemSpec: "tfm1/PackageReference1/3.0.1",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.Type, "Package" },
                    { MetadataKeys.IsTopLevelDependency, "True" },
                    { MetadataKeys.IsImplicitlyDefined, "True" },
                    { MetadataKeys.SDKPackageItemSpec, "tfm1/PackageReference1/3.0.1" },
                    { MetadataKeys.Name, "PackageReference1" },
                    { MetadataKeys.OriginalItemSpec, "PackageReference1" },
                    { MetadataKeys.Path, @"x:\folder\subfolder" },
                    { MetadataKeys.Version, @"3.0.1" }

                });

            var packageReference1_otherTFM = new MockTaskItem(
                itemSpec: "tfm2/PackageReference1/3.0.1",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.Type, "Package" },
                    { MetadataKeys.IsTopLevelDependency, "True" },
                    { MetadataKeys.IsImplicitlyDefined, "True" },
                    { MetadataKeys.SDKPackageItemSpec, "tfm2/PackageReference1/3.0.1" },
                    { MetadataKeys.Name, "PackageReference1" },
                    { MetadataKeys.OriginalItemSpec, "PackageReference1" },
                    { MetadataKeys.Path, @"x:\folder\subfolder\tfm2" }

                });

            var packageReference_Target = new MockTaskItem(
                itemSpec: "packageReference_Target",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.Type, "Target" },
                    { MetadataKeys.IsTopLevelDependency, "True" },
                    { MetadataKeys.IsImplicitlyDefined, "True" },
                    { MetadataKeys.Name, "packageReference_Target" }
                });

            var packageReference_Unknown = new MockTaskItem(
                itemSpec: "packageReference_Unknown",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.Type, "Xxx" },
                    { MetadataKeys.IsTopLevelDependency, "True" },
                    { MetadataKeys.IsImplicitlyDefined, "True" },
                    { MetadataKeys.Name, "packageReference_Unknown" }
                });

            var packageReference_NotTopLevel = new MockTaskItem(
                itemSpec: "packageReference_NotTopLevel",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.Type, "Package" },
                    { MetadataKeys.IsTopLevelDependency, "False" },
                    { MetadataKeys.IsImplicitlyDefined, "True" },
                    { MetadataKeys.Name, "packageReference_NotTopLevel" }
                });

            var packageReference_NotTopLevel2 = new MockTaskItem(
                itemSpec: "packageReference_NotTopLevel2",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.Type, "Package" },
                    { MetadataKeys.IsTopLevelDependency, "xxxx" },
                    { MetadataKeys.IsImplicitlyDefined, "True" },
                    { MetadataKeys.Name, "packageReference_NotTopLevel2" }
                });

            var packageReference_NotImplicit = new MockTaskItem(
                itemSpec: "packageReference_NotImplicit",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.Type, "Package" },
                    { MetadataKeys.IsTopLevelDependency, "True" },
                    { MetadataKeys.IsImplicitlyDefined, "False" },
                    { MetadataKeys.Name, "packageReference_NotImplicit" }
                });

            var packageReference_NotImplicit2 = new MockTaskItem(
                itemSpec: "packageReference_NotImplicit2",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.Type, "Package" },
                    { MetadataKeys.IsTopLevelDependency, "True" },
                    { MetadataKeys.IsImplicitlyDefined, "Xxxx" },
                    { MetadataKeys.Name, "packageReference_NotImplicit2" }
                });

            var task = new CollectResolvedSDKReferencesDesignTime();
            task.ResolvedSdkReferences = new[] {
                sdkReference1,
                sdkReference2
            };
            task.DependenciesDesignTime = new ITaskItem[] {
                packageReference1,
                packageReference1_otherTFM,
                packageReference_Target,
                packageReference_Unknown,
                packageReference_NotTopLevel,
                packageReference_NotTopLevel2,
                packageReference_NotImplicit,
                packageReference_NotImplicit2
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().BeTrue();
            task.ResolvedSDKReferencesDesignTime.Count().Should().Be(3);

            VerifyTaskItem(sdkReference1, task.ResolvedSDKReferencesDesignTime[0]);
            VerifyTaskItem(sdkReference2, task.ResolvedSDKReferencesDesignTime[1]);

            var path = packageReference1.GetMetadata(MetadataKeys.Path);
            packageReference1.RemoveMetadata(MetadataKeys.Path);
            packageReference1.SetMetadata(MetadataKeys.SDKRootFolder, path);
            VerifyTaskItem(packageReference1, task.ResolvedSDKReferencesDesignTime[2], "PackageReference1");
        }

        private void VerifyTaskItem(ITaskItem input, ITaskItem output, string expectedItemSpec = null)
        {
            // remove unnecessary metadata to keep only ones that would be in result task items
            var removeMetadata = new[] {
                MetadataKeys.IsImplicitlyDefined,
                MetadataKeys.IsTopLevelDependency,
                MetadataKeys.Type
            };

            foreach(var rm in removeMetadata)
            {
                output.RemoveMetadata(rm);
                input.RemoveMetadata(rm);
            }
            if (expectedItemSpec != null)
            {
                expectedItemSpec.Should().Be(output.ItemSpec);
            }

            foreach (var metadata in input.MetadataNames)
            {
                input.GetMetadata(metadata.ToString()).Should().Be(output.GetMetadata(metadata.ToString()));
            }
        }
    }
}
