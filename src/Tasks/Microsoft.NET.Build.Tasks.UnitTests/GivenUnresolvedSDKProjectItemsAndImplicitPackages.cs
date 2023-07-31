// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenUnresolvedSDKProjectItemsAndImplicitPackages
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
                itemSpec: "PackageReference1",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.IsImplicitlyDefined, "True" },
                    { MetadataKeys.SDKPackageItemSpec, "" },
                    { MetadataKeys.Name, "PackageReference1" }
                });

            var packageReference2 = new MockTaskItem(
                itemSpec: "PackageReference2",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.IsImplicitlyDefined, "aaa" },
                    { MetadataKeys.Version, "3.0.1" }
                });

            var packageReference3 = new MockTaskItem(
                itemSpec: "PackageReference3",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.IsImplicitlyDefined, "False" },
                    { MetadataKeys.Version, "1.0.1" }
                });
            var defaultImplicitPackage1 = new MockTaskItem(
                itemSpec: "DefaultImplicitPackage1",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.SDKPackageItemSpec, "" },
                    { MetadataKeys.Name, "DefaultImplicitPackage1" },
                    { MetadataKeys.Version, "1.2.3" }
                });

            var task = new CollectSDKReferencesDesignTime();
            task.SdkReferences = new[] {
                sdkReference1,
                sdkReference2
            };
            task.PackageReferences = new ITaskItem[] {
                packageReference1,
                packageReference2,
                packageReference3,
                defaultImplicitPackage1
            };
            task.DefaultImplicitPackages = "DefaultImplicitPackage1;SomeOtherImplicitPackage";

            // Act
            var result = task.Execute();

            // Assert
            result.Should().BeTrue();
            task.SDKReferencesDesignTime.Count().Should().Be(4);

            VerifyTaskItem(sdkReference1, task.SDKReferencesDesignTime[0]);
            VerifyTaskItem(sdkReference2, task.SDKReferencesDesignTime[1]);
            VerifyTaskItem(packageReference1, task.SDKReferencesDesignTime[2], true);
            VerifyTaskItem(defaultImplicitPackage1, task.SDKReferencesDesignTime[3], true);
        }

        private void VerifyTaskItem(ITaskItem input, ITaskItem output, bool checkImplicit = false)
        {
            // remove unnecessary metadata to keep only ones that would be in result task items
            var removeMetadata = new[] { MetadataKeys.IsImplicitlyDefined };
            foreach (var rm in removeMetadata)
            {
                input.RemoveMetadata(rm);
            }

            input.ItemSpec.Should().Be(output.ItemSpec);
            foreach (var metadata in input.MetadataNames)
            {
                input.GetMetadata(metadata.ToString()).Should().Be(output.GetMetadata(metadata.ToString()));
            }

            if (checkImplicit)
            {
                output.GetMetadata(MetadataKeys.IsImplicitlyDefined).Should().Be("True");
            }
        }
    }
}
