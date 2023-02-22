// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    public sealed class FindInvalidProjectReferences_Tests
    {
        /// <summary>
        /// Verify FindInvalidProjectReferences for several target platform monikers
        /// </summary>
        [Fact]
        public void VerifyFindInvalidProjectReferences()
        {
            // Create the engine.
            MockEngine engine = new MockEngine();

            FindInvalidProjectReferences t = new FindInvalidProjectReferences();
            t.TargetPlatformVersion = "8.0";
            t.TargetPlatformIdentifier = "Windows";
            Dictionary<string, string> proj1 = new Dictionary<string, string>();
            proj1["TargetPlatformMoniker"] = "Windows, Version=7.0";

            Dictionary<string, string> proj2 = new Dictionary<string, string>();
            proj2["TargetPlatformMoniker"] = "Windows, Version=8.0";

            Dictionary<string, string> proj3 = new Dictionary<string, string>();
            proj3["TargetPlatformMoniker"] = "Windows, Version=8.1";

            Dictionary<string, string> proj4 = new Dictionary<string, string>();
            proj4["TargetPlatformMoniker"] = "Windows, Version=8.2";

            t.ProjectReferences = new TaskItem[] { new TaskItem("proj1.proj", proj1), new TaskItem("proj2.proj", proj2), new TaskItem("proj3.proj", proj3), new TaskItem("proj4.proj", proj4) };
            t.BuildEngine = engine;
            bool succeeded = t.Execute();
            Assert.True(succeeded);

            string warning1 = ResourceUtilities.FormatResourceStringStripCodeAndKeyword("FindInvalidProjectReferences.WarnWhenVersionIsIncompatible", "Windows", "8.0", "proj1.proj", "Windows, Version=7.0");
            engine.AssertLogDoesntContain(warning1);

            string warning2 = ResourceUtilities.FormatResourceStringStripCodeAndKeyword("FindInvalidProjectReferences.WarnWhenVersionIsIncompatible", "Windows", "8.0", "proj2.proj", "Windows, Version=8.0");
            engine.AssertLogDoesntContain(warning2);

            string warning3 = ResourceUtilities.FormatResourceStringStripCodeAndKeyword("FindInvalidProjectReferences.WarnWhenVersionIsIncompatible", "Windows", "8.0", "proj3.proj", "Windows, Version=8.1");
            engine.AssertLogContains(warning3);

            string warning4 = ResourceUtilities.FormatResourceStringStripCodeAndKeyword("FindInvalidProjectReferences.WarnWhenVersionIsIncompatible", "Windows", "8.0", "proj4.proj", "Windows, Version=8.2");
            engine.AssertLogContains(warning4);

            Assert.Equal(2, t.InvalidReferences.Length);
            Assert.Equal("proj3.proj", t.InvalidReferences[0].ItemSpec);
            Assert.Equal("proj4.proj", t.InvalidReferences[1].ItemSpec);
        }
    }
}
