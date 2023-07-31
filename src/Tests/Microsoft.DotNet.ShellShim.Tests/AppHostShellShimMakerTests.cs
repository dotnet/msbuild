// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.HostModel.AppHost;

namespace Microsoft.DotNet.ShellShim.Tests
{
    public class AppHostShellShimMakerTests : SdkTest
    {
        const ushort WindowsGUISubsystem = 0x2;

        public AppHostShellShimMakerTests(ITestOutputHelper log) : base(log)
        {
        }

        [WindowsOnlyFact]
        public void WhenCallWithWpfDllItCanCreateShimWithWindowsGraphicalUserInterfaceBitSet()
        {
            string shimPath = CreateApphostAndReturnShimPath();

            PEUtils.GetWindowsGraphicalUserInterfaceBit(shimPath).Should().Be(WindowsGUISubsystem);
        }

        [UnixOnlyFact]
        public void GivenNonWindowsMachineWhenCallWithWpfDllItCanCreateShimWithoutThrow()
        {
            Action a = () => CreateApphostAndReturnShimPath();
            a.Should().NotThrow("It should skip copying PE bits without throw");
        }

        private static string CreateApphostAndReturnShimPath()
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDirectory);
            var appHostShellShimMaker = new AppHostShellShimMaker(Path.Combine(TestContext.Current.ToolsetUnderTest.SdkFolderUnderTest, "AppHostTemplate"));
            string shimPath = Path.Combine(tempDirectory, Path.GetRandomFileName());

            appHostShellShimMaker.CreateApphostShellShim(
                new FilePath(Path.GetFullPath(Path.Combine("WpfBinaryTestAsssets", "testwpf.dll"))),
                new FilePath(shimPath));
            return shimPath;
        }
    }
}
