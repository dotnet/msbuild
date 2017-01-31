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
using System.Net;
using System.Threading;
using System.Xml;

using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Xunit;

using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;
using ProjectHelpers = Microsoft.Build.UnitTests.BackEnd.ProjectHelpers;

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

        /// <summary>
        /// Basic verification -- with TreatAsLocalProperty set, but to a different property than is being passed as a global, the 
        /// global property overrides the local property.  
        /// </summary>
        [Fact]
        public void MultipleForwardSlashesShouldNotGetCollapsedWhenPathLooksLikeUnixPath()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
                             <Project>
                                    <PropertyGroup>
                                            <P>/tmp/a//x\\c;ba://</P>
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


            IDictionary<string, string> globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            globalProperties.Add("GP", @"/tmp/a//x\\c;ba://");

            Project project = new Project(XmlReader.Create(new StringReader(content)), globalProperties, null);

            MockLogger logger = new MockLogger();
            bool result = project.Build(logger);
            Assert.Equal(true, result);

            var expectedString = NativeMethodsShared.IsWindows ? @"/tmp/a//x\\c;ba://" : @"/tmp/a//x//c;ba://";

            logger.AssertLogContains($"GP[{expectedString}]");
            logger.AssertLogContains($"P[{expectedString}]");
            logger.AssertLogContains($"I[{expectedString}]");
            logger.AssertLogContains($"T[{expectedString}]");

            Assert.Equal(expectedString, project.GetPropertyValue("GP"));
            Assert.Equal(expectedString, project.GetPropertyValue("P"));
            Assert.Equal(expectedString, string.Join(";", project.Items.Select(i => i.EvaluatedInclude)));
        }
    }
}
