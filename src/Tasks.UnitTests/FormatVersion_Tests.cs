// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Tasks;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    public sealed class FormatVersion_Tests
    {
        /// <summary>
        /// An undefined (null or empty) Version yields the default "1.0.0.0".
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void UndefinedVersionYieldsDefault(string version)
        {
            FormatVersion t = new FormatVersion
            {
                BuildEngine = new MockEngine(),
                Version = version,
                Revision = 5,
            };

            t.Execute().ShouldBeTrue();
            t.OutputVersion.ShouldBe("1.0.0.0");
        }

        /// <summary>
        /// A wildcard Version has its trailing '*' replaced by the Revision value.
        /// </summary>
        [Fact]
        public void WildcardVersionIsCombinedWithRevision()
        {
            FormatVersion t = new FormatVersion
            {
                BuildEngine = new MockEngine(),
                Version = "1.0.0.*",
                Revision = 5,
            };

            t.Execute().ShouldBeTrue();
            t.OutputVersion.ShouldBe("1.0.0.5");
        }

        /// <summary>
        /// A fully specified (non-wildcard) Version is passed through unchanged, regardless of Revision.
        /// </summary>
        [Fact]
        public void FullVersionIsPassedThroughUnchanged()
        {
            FormatVersion t = new FormatVersion
            {
                BuildEngine = new MockEngine(),
                Version = "1.0.0.0",
                Revision = 42,
            };

            t.Execute().ShouldBeTrue();
            t.OutputVersion.ShouldBe("1.0.0.0");
        }

        /// <summary>
        /// FormatType "Path" replaces the dots in the resulting version with underscores.
        /// </summary>
        [Fact]
        public void PathFormatTypeReplacesDotsWithUnderscores()
        {
            FormatVersion t = new FormatVersion
            {
                BuildEngine = new MockEngine(),
                Version = "1.0.0.0",
                FormatType = "Path",
            };

            t.Execute().ShouldBeTrue();
            t.OutputVersion.ShouldBe("1_0_0_0");
        }

        /// <summary>
        /// FormatType "Path" is applied after the wildcard revision substitution.
        /// </summary>
        [Fact]
        public void PathFormatTypeAppliesAfterWildcardSubstitution()
        {
            FormatVersion t = new FormatVersion
            {
                BuildEngine = new MockEngine(),
                Version = "1.0.0.*",
                Revision = 7,
                FormatType = "Path",
            };

            t.Execute().ShouldBeTrue();
            t.OutputVersion.ShouldBe("1_0_0_7");
        }

        /// <summary>
        /// FormatType is parsed case-insensitively.
        /// </summary>
        [Fact]
        public void FormatTypeIsCaseInsensitive()
        {
            FormatVersion t = new FormatVersion
            {
                BuildEngine = new MockEngine(),
                Version = "1.0.0.0",
                FormatType = "path",
            };

            t.Execute().ShouldBeTrue();
            t.OutputVersion.ShouldBe("1_0_0_0");
        }

        /// <summary>
        /// The explicit "Version" FormatType keeps the dotted version format.
        /// </summary>
        [Fact]
        public void VersionFormatTypeKeepsDots()
        {
            FormatVersion t = new FormatVersion
            {
                BuildEngine = new MockEngine(),
                Version = "1.0.0.0",
                FormatType = "Version",
            };

            t.Execute().ShouldBeTrue();
            t.OutputVersion.ShouldBe("1.0.0.0");
        }

        /// <summary>
        /// An unrecognized FormatType fails the task and logs MSB3098.
        /// </summary>
        [Fact]
        public void InvalidFormatTypeFailsWithError()
        {
            MockEngine engine = new MockEngine();
            FormatVersion t = new FormatVersion
            {
                BuildEngine = engine,
                Version = "1.0.0.0",
                FormatType = "Bogus",
            };

            t.Execute().ShouldBeFalse();
            engine.AssertLogContains("MSB3098");
        }
    }
}
