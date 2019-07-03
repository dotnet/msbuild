using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.UnitTests.ResolveAssemblyReference_Tests
{
    /// <summary>
    /// Unit tests for the InstalledSDKResolver task.
    /// </summary>
    public sealed class InstalledSDKResolverFixture : ResolveAssemblyReferenceTestFixture
    {
        public InstalledSDKResolverFixture(ITestOutputHelper output) : base(output)
        {
        }

        /// <summary>
        /// Verify that we do not find the winmd file even if it on the search path if the sdkname does not match something passed into the ResolvedSDKs property.
        /// </summary>
        [Fact]
        public void SDkNameNotInResolvedSDKListButOnSearchPath()
        {
            // Create the engine.
            MockEngine engine = new MockEngine(_output);
            TaskItem taskItem = new TaskItem(@"SDKWinMD");
            taskItem.SetMetadata("SDKName", "NotInstalled, Version=1.0");

            TaskItem[] assemblies = new TaskItem[] { taskItem };

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblies;
            t.SearchPaths = new String[] { @"C:\FakeSDK\References" };
            bool succeeded = Execute(t);

            Assert.True(succeeded);
            Assert.Empty(t.ResolvedFiles);

            Assert.Equal(0, engine.Errors);
            Assert.Equal(1, engine.Warnings);
        }

        /// <summary>
        /// Verify when we are trying to match a name which is the reference assembly directory
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        public void SDkNameMatchInRADirectory()
        {
            ResolveSDKFromRefereneAssemblyLocation("DebugX86SDKWinMD", @"C:\FakeSDK\References\Debug\X86\DebugX86SDKWinMD.Winmd", _output);
            ResolveSDKFromRefereneAssemblyLocation("DebugNeutralSDKWinMD", @"C:\FakeSDK\References\Debug\Neutral\DebugNeutralSDKWinMD.Winmd", _output);
            ResolveSDKFromRefereneAssemblyLocation("x86SDKWinMD", @"C:\FakeSDK\References\CommonConfiguration\x86\x86SDKWinMD.Winmd", _output);
            ResolveSDKFromRefereneAssemblyLocation("NeutralSDKWinMD", @"C:\FakeSDK\References\CommonConfiguration\Neutral\NeutralSDKWinMD.Winmd", _output);
            ResolveSDKFromRefereneAssemblyLocation("SDKReference", @"C:\FakeSDK\References\Debug\X86\SDKReference.dll", _output);
            ResolveSDKFromRefereneAssemblyLocation("DebugX86SDKRA", @"C:\FakeSDK\References\Debug\X86\DebugX86SDKRA.dll", _output);
            ResolveSDKFromRefereneAssemblyLocation("DebugNeutralSDKRA", @"C:\FakeSDK\References\Debug\Neutral\DebugNeutralSDKRA.dll", _output);
            ResolveSDKFromRefereneAssemblyLocation("x86SDKRA", @"C:\FakeSDK\References\CommonConfiguration\x86\x86SDKRA.dll", _output);
            ResolveSDKFromRefereneAssemblyLocation("NeutralSDKRA", @"C:\FakeSDK\References\CommonConfiguration\Neutral\NeutralSDKRA.dll", _output);
        }

        private static void ResolveSDKFromRefereneAssemblyLocation(string referenceName, string expectedPath, ITestOutputHelper output)
        {
            // Create the engine.
            MockEngine engine = new MockEngine(output);
            TaskItem taskItem = new TaskItem(referenceName);
            taskItem.SetMetadata("SDKName", "FakeSDK, Version=1.0");

            TaskItem resolvedSDK = new TaskItem(@"C:\FakeSDK");
            resolvedSDK.SetMetadata("SDKName", "FakeSDK, Version=1.0");
            resolvedSDK.SetMetadata("TargetedSDKConfiguration", "Debug");
            resolvedSDK.SetMetadata("TargetedSDKArchitecture", "X86");

            TaskItem[] assemblies = new TaskItem[] { taskItem };

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblies;
            t.ResolvedSDKReferences = new ITaskItem[] { resolvedSDK };
            t.SearchPaths = new String[] { @"C:\SomeOtherPlace" };
            bool succeeded = Execute(t);

            Assert.True(succeeded);
            Assert.Single(t.ResolvedFiles);
            Assert.Equal(0, engine.Errors);
            Assert.Equal(0, engine.Warnings);
            Assert.Equal(expectedPath, t.ResolvedFiles[0].ItemSpec, true);
        }
    }
}
