// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Tests for evaluation</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Xml;

using Microsoft.Build.Evaluation;
using Microsoft.Build.Shared;
using Xunit;

namespace Microsoft.Build.UnitTests.Evaluation
{
    /// <summary>
    /// Tests mainly for how evaluation normalizes input for cross-platform paths
    /// </summary>
    public class EvaluatorFileNormalization_Tests : IDisposable
    {
        public EvaluatorFileNormalization_Tests()
        {
            ProjectCollection.GlobalProjectCollection.UnloadAllProjects();
            GC.Collect();
        }

        /// <summary>
        /// Cleanup
        /// </summary>
        public void Dispose()
        {
            ProjectCollection.GlobalProjectCollection.UnloadAllProjects();
            GC.Collect();
        }

        [Theory]
        [InlineData(@"/bin/a//x\\c;ba://", new string[0], true)]
        [InlineData(@"/shouldNotExistAtRootLevel/a//x\\c;ba://", new string[0], false)]
        [InlineData(@"a/b//x\\c;ba://", new[] { "b/a.txt" }, false)]
        [InlineData(@"a/b//x\\c;ba://", new[] { "a/a.txt" }, true)]
        public void MultipleForwardSlashesShouldNotGetCollapsedWhenPathLooksLikeUnixPath(string stringLookingLikeUnixPath, string[] intermediateFiles, bool firstPathFragmentExists)
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
                             <Project>
                                    <PropertyGroup>
                                            <P>{0}</P>
                                    </PropertyGroup>
                                    <ItemGroup>
                                            <I Include=""$(p)""/>
                                    </ItemGroup>

                                    <Target Name=""Build"">
                                            <ItemGroup>
                                                    <T Include=""$(p)""/>
                                            </ItemGroup>
                                            <Message Text=""GP[$(GP)]"" Importance=""High""/>
                                            <Message Text=""P[$(P)]"" Importance=""High""/>
                                            <Message Text=""I[@(I)]"" Importance=""High""/>
                                            <Message Text=""T[@(T)]"" Importance=""High""/>
                                    </Target>
                            </Project>");

            content = string.Format(content, stringLookingLikeUnixPath);

            using (var testFiles = new Helpers.TestProjectWithFiles(content, intermediateFiles))
            {
                IDictionary<string, string> globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                globalProperties.Add("GP", stringLookingLikeUnixPath);

                Project project = new Project(testFiles.ProjectFile, globalProperties, null);

                MockLogger logger = new MockLogger();
                bool result = project.Build(logger);
                Assert.Equal(true, result);

                var expectedString = firstPathFragmentExists
                    ? NativeMethodsShared.IsWindows ? stringLookingLikeUnixPath : stringLookingLikeUnixPath.ToSlash()
                    : stringLookingLikeUnixPath;

                logger.AssertLogContains($"GP[{expectedString}]");
                logger.AssertLogContains($"P[{expectedString}]");
                logger.AssertLogContains($"I[{expectedString}]");
                logger.AssertLogContains($"T[{expectedString}]");

                Assert.Equal(expectedString, project.GetPropertyValue("GP"));
                Assert.Equal(expectedString, project.GetPropertyValue("P"));
                Assert.Equal(expectedString, string.Join(";", project.Items.Select(i => i.EvaluatedInclude)));
            }
        }

        [Fact]
        public void DoubleSlashCausesItemMissmatchForExcludeAndRemove()
        {
            var content = ObjectModelHelpers.CleanupFileContents(
@"<Project>
    <ItemGroup>
        <I Include='a//b//**' Exclude='a/b/*.foo'/>
        <I2 Include='a/b/**' Exclude='a//b/*.foo'/>
    </ItemGroup>

    <Target Name='Build'>
        <ItemGroup>
            <T Include='a/b/**'/>
            <T Remove='a//b/*.foo'/>
            <T Remove='a/b//*.foo'/>
        </ItemGroup>
        <Message Text='I[@(I)]' Importance='High'/>
        <Message Text='I2[@(I2)]' Importance='High'/>
        <Message Text='T[@(T)]' Importance='High'/>
    </Target>
</Project>");

            using (var testFiles = new Helpers.TestProjectWithFiles(content, new[] { "a/b/a.cs", "a/b/b.foo" }))
            {
                var project = new Project(testFiles.ProjectFile);

                var logger = new MockLogger();
                var result = project.Build(logger);
                Assert.Equal(true, result);

                var expectedI = @"a//b//a.cs;a//b//b.foo";
                var expectedI2 = @"a/b/a.cs;a/b/b.foo";

                Assert.Equal(expectedI, string.Join(";", project.GetItems("I").Select(_ => _.EvaluatedInclude)));
                Assert.Equal(expectedI2, string.Join(";", project.GetItems("I2").Select(_ => _.EvaluatedInclude)));

                logger.AssertLogContains($"I[{expectedI}]");
                logger.AssertLogContains($"I2[{expectedI2}]");
                logger.AssertLogContains($"T[{expectedI2}]");
            }
        }
    }
}
