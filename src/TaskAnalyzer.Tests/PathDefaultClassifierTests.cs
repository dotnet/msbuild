// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Shouldly;

namespace Microsoft.Build.TaskAuthoring.Analyzer.Tests
{
    [TestClass]
    public class PathDefaultClassifierTests
    {
        private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        [TestMethod]
        [DataRow("")]
        [DataRow("obj")]
        [DataRow("obj/generated")]
        [DataRow(@"sub\dir")]
        [DataRow("./relative")]
        public void RelativeForms_AreNotFullyQualified(string value)
        {
            PathDefaultClassifier.IsFullyQualifiedPath(value).ShouldBeFalse();
        }

        [TestMethod]
        public void UnixRoot_IsFullyQualifiedOnUnixOnly()
        {
            // A leading '/' is absolute on Unix; on Windows it is only rooted to the current drive, so it is
            // NOT fully qualified there.
            PathDefaultClassifier.IsFullyQualifiedPath("/etc/config").ShouldBe(!IsWindows);
        }

        [TestMethod]
        public void WindowsDriveAbsolute_IsFullyQualifiedOnWindowsOnly()
        {
            // "C:/x" and "C:\x" are absolute on Windows; on Unix they are ordinary relative paths, matching how
            // AbsolutePath would root them.
            PathDefaultClassifier.IsFullyQualifiedPath(@"C:\temp\out").ShouldBe(IsWindows);
            PathDefaultClassifier.IsFullyQualifiedPath("C:/temp/out").ShouldBe(IsWindows);
        }

        [TestMethod]
        public void WindowsUncPath_IsFullyQualifiedOnWindowsOnly()
        {
            PathDefaultClassifier.IsFullyQualifiedPath(@"\\server\share").ShouldBe(IsWindows);
        }

        [TestMethod]
        public void WindowsDriveRelative_IsNeverFullyQualified()
        {
            // "C:foo" (no separator after the colon) is drive-relative — its meaning depends on the drive's
            // current directory — so it is not fully qualified on any OS.
            PathDefaultClassifier.IsFullyQualifiedPath("C:foo").ShouldBeFalse();
        }

        [TestMethod]
        public void RelativeDefault_IsTrueForPlausibleRelativePathsOnly()
        {
            PathDefaultClassifier.IsRelativePathDefault("obj").ShouldBeTrue();
            PathDefaultClassifier.IsRelativePathDefault("").ShouldBeFalse();

            // A fully-qualified default (on this OS) is not a relative default.
            string absolute = IsWindows ? @"C:\out" : "/out";
            PathDefaultClassifier.IsRelativePathDefault(absolute).ShouldBeFalse();
        }
    }
}
