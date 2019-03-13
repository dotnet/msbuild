using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.UnitTests.ResolveAssemblyReference_Tests
{
    /// <summary>
    /// Unit tests for the ResolveAssemblyReference task.
    /// </summary>
    public sealed class ReferenceTests : ResolveAssemblyReferenceTestFixture
    {
        public ReferenceTests(ITestOutputHelper output) : base(output)
        {
        }

        /// <summary>
        /// Check to make sure if, the specific version metadata is set on a primary reference, that true is returned from CheckForSpecificMetadataOnParent
        /// </summary>
        [Fact]
        public void CheckForSpecificMetadataOnParent()
        {
            Reference reference = new Reference(isWinMDFile, fileExists, getRuntimeVersion);
            ITaskItem taskItem = new TaskItem("TestReference");
            taskItem.SetMetadata("SpecificVersion", "true");
            reference.MakePrimaryAssemblyReference(taskItem, true, ".dll");
            Assert.True(reference.CheckForSpecificVersionMetadataOnParentsReference(false));
        }

        /// <summary>
        /// Check to make sure if, the specific version metadata is set on all primary references which a dependency depends on, that true is returned from CheckForSpecificMetadataOnParent
        /// </summary>
        [Fact]
        public void CheckForSpecificMetadataOnParentAllParentsHaveMetadata()
        {
            Reference primaryReference1 = new Reference(isWinMDFile, fileExists, getRuntimeVersion);
            ITaskItem taskItem = new TaskItem("TestPrimary1");
            taskItem.SetMetadata("SpecificVersion", "true");
            primaryReference1.MakePrimaryAssemblyReference(taskItem, true, ".dll");
            primaryReference1.FullPath = "FullPath";

            Reference primaryReference2 = new Reference(isWinMDFile, fileExists, getRuntimeVersion);
            ITaskItem taskItem2 = new TaskItem("TestPrimary2");
            taskItem2.SetMetadata("SpecificVersion", "true");
            primaryReference2.MakePrimaryAssemblyReference(taskItem2, true, ".dll");
            primaryReference2.FullPath = "FullPath";

            Reference dependentReference = new Reference(isWinMDFile, fileExists, getRuntimeVersion);
            dependentReference.FullPath = "FullPath";

            dependentReference.MakeDependentAssemblyReference(primaryReference1);
            dependentReference.MakeDependentAssemblyReference(primaryReference2);

            Assert.True(dependentReference.CheckForSpecificVersionMetadataOnParentsReference(false));
        }

        /// <summary>
        /// Check to make sure if, the specific version metadata is set on some primary references which a dependency depends on, that false is returned from CheckForSpecificMetadataOnParent
        /// </summary>
        [Fact]
        public void CheckForSpecificMetadataOnParentNotAllParentsHaveMetadata()
        {
            Reference primaryReference1 = new Reference(isWinMDFile, fileExists, getRuntimeVersion);
            ITaskItem taskItem = new TaskItem("TestPrimary1");
            taskItem.SetMetadata("SpecificVersion", "false");
            primaryReference1.MakePrimaryAssemblyReference(taskItem, false, ".dll");
            primaryReference1.FullPath = "FullPath";

            Reference primaryReference2 = new Reference(isWinMDFile, fileExists, getRuntimeVersion);
            ITaskItem taskItem2 = new TaskItem("TestPrimary2");
            taskItem2.SetMetadata("SpecificVersion", "true");
            primaryReference2.MakePrimaryAssemblyReference(taskItem2, true, ".dll");
            primaryReference2.FullPath = "FullPath";

            Reference dependentReference = new Reference(isWinMDFile, fileExists, getRuntimeVersion);
            dependentReference.FullPath = "FullPath";

            dependentReference.MakeDependentAssemblyReference(primaryReference1);
            dependentReference.MakeDependentAssemblyReference(primaryReference2);

            Assert.False(dependentReference.CheckForSpecificVersionMetadataOnParentsReference(false)); // "Expected check to return false but it returned true."
        }

        /// <summary>
        /// Check to make sure if, the specific version metadata is set on some primary references which a dependency depends on, that false is returned from CheckForSpecificMetadataOnParent
        /// </summary>
        [Fact]
        public void CheckForSpecificMetadataOnParentNotAllParentsHaveMetadata2()
        {
            Reference primaryReference1 = new Reference(isWinMDFile, fileExists, getRuntimeVersion);
            ITaskItem taskItem = new TaskItem("TestPrimary1");
            primaryReference1.MakePrimaryAssemblyReference(taskItem, false, ".dll");
            primaryReference1.FullPath = "FullPath";

            Reference primaryReference2 = new Reference(isWinMDFile, fileExists, getRuntimeVersion);
            ITaskItem taskItem2 = new TaskItem("TestPrimary2");
            taskItem2.SetMetadata("SpecificVersion", "true");
            primaryReference2.MakePrimaryAssemblyReference(taskItem2, true, ".dll");
            primaryReference2.FullPath = "FullPath";

            Reference dependentReference = new Reference(isWinMDFile, fileExists, getRuntimeVersion);
            dependentReference.FullPath = "FullPath";

            dependentReference.MakeDependentAssemblyReference(primaryReference1);
            dependentReference.MakeDependentAssemblyReference(primaryReference2);

            Assert.False(dependentReference.CheckForSpecificVersionMetadataOnParentsReference(false)); // "Expected check to return false but it returned true."
        }

        /// <summary>
        /// Check to make sure if, the specific version metadata is set on some primary references which a dependency depends on, that true is returned from CheckForSpecificMetadataOnParent if the anyParentHasmetadata parameter is set to true.
        /// </summary>
        [Fact]
        public void CheckForSpecificMetadataOnParentNotAllParentsHaveMetadata3()
        {
            Reference primaryReference1 = new Reference(isWinMDFile, fileExists, getRuntimeVersion);
            ITaskItem taskItem = new TaskItem("TestPrimary1");
            taskItem.SetMetadata("SpecificVersion", "false");
            primaryReference1.MakePrimaryAssemblyReference(taskItem, false, ".dll");
            primaryReference1.FullPath = "FullPath";

            Reference primaryReference2 = new Reference(isWinMDFile, fileExists, getRuntimeVersion);
            ITaskItem taskItem2 = new TaskItem("TestPrimary2");
            taskItem2.SetMetadata("SpecificVersion", "true");
            primaryReference2.MakePrimaryAssemblyReference(taskItem2, true, ".dll");
            primaryReference2.FullPath = "FullPath";

            Reference dependentReference = new Reference(isWinMDFile, fileExists, getRuntimeVersion);
            dependentReference.FullPath = "FullPath";

            dependentReference.MakeDependentAssemblyReference(primaryReference1);
            dependentReference.MakeDependentAssemblyReference(primaryReference2);

            Assert.True(dependentReference.CheckForSpecificVersionMetadataOnParentsReference(true)); // "Expected check to return false but it returned true."
        }
    }
}
