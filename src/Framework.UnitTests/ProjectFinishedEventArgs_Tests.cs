// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Unit tests for ProjectFinishedEventArgs</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Profiler;
using Microsoft.Build.UnitTests.BackEnd;
using Xunit;
using Xunit.Extensions;

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Verify the functioning of the ProjectFinishedEventArgs class.
    /// </summary>
    public class ProjectFinishedEventArgs_Tests
    {
        /// <summary>
        /// Default event to use in tests.
        /// </summary>
        private ProjectFinishedEventArgs _baseProjectFinishedEvent = new ProjectFinishedEventArgs("Message", "HelpKeyword", "ProjectFile", true);
        
        /// <summary>
        /// Trivially exercise event args default ctors to boost Frameworks code coverage
        /// </summary>
        [Fact]
        public void EventArgsCtors()
        {
            ProjectFinishedEventArgs projectFinishedEvent = new ProjectFinishedEventArgs2();
            projectFinishedEvent = new ProjectFinishedEventArgs("Message", "HelpKeyword", "ProjectFile", true);
            projectFinishedEvent = new ProjectFinishedEventArgs("Message", "HelpKeyword", "ProjectFile", true, DateTime.Now);
            projectFinishedEvent = new ProjectFinishedEventArgs(null, null, null, true);
            projectFinishedEvent = new ProjectFinishedEventArgs(null, null, null, true, DateTime.Now);
        }

        /// <summary>
        /// Roundtrip serialization tests for <see cref="ProfilerResult"/>
        /// </summary>
        [MemberData(nameof(GetProfilerResults))]
        [Theory]
        public void ProfilerResultRoundTrip(ProfilerResult profilerResult)
        {
            var writeTranslator = TranslationHelpers.GetWriteTranslator();
            ProfilerResult deserializedResult;

#if FEATURE_BINARY_SERIALIZATION
            writeTranslator.TranslateDotNet(ref profilerResult);
#else
            NodePacketTranslator.ProfilerResultTranslator.Translate(writeTranslator, ref profilerResult);
#endif

            var readTranslator = TranslationHelpers.GetReadTranslator();

#if FEATURE_BINARY_SERIALIZATION
            readTranslator.TranslateDotNet(ref deserializedResult);
#else
            NodePacketTranslator.ProfilerResultTranslator.Translate(readTranslator, ref deserializedResult);
#endif

            Assert.Equal(deserializedResult, profilerResult);
        }

        private static IEnumerable<object[]> GetProfilerResults()
        {
            yield return new object[] { new ProfilerResult(new Dictionary<EvaluationLocation, ProfiledLocation>()) };

            yield return new object[] { new ProfilerResult(new Dictionary<EvaluationLocation, ProfiledLocation>
            {
                {new EvaluationLocation(EvaluationPass.TotalEvaluation, "1", "myFile", 42, "elementName", "elementOrCondition", true), new ProfiledLocation(TimeSpan.MaxValue, TimeSpan.MinValue, 2)},
                {new EvaluationLocation(EvaluationPass.Targets, "1", null, null, null, null, false), new ProfiledLocation(TimeSpan.MaxValue, TimeSpan.MinValue, 2)},
                {new EvaluationLocation(EvaluationPass.LazyItems, "2", null, null, null, null, false), new ProfiledLocation(TimeSpan.Zero, TimeSpan.Zero, 0)}
            }) };

            var element = new ProjectRootElement(
                XmlReader.Create(new MemoryStream(Encoding.UTF8.GetBytes(
                    "<Project xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\"/>"))),
                new ProjectRootElementCache(false), false, false);

            yield return new object[] { new ProfilerResult(new Dictionary<EvaluationLocation, ProfiledLocation>
            {
                {new EvaluationLocation(EvaluationPass.UsingTasks, "1", "myFile", 42, "conditionCase"), new ProfiledLocation(TimeSpan.MaxValue, TimeSpan.MinValue, 2)},
                {new EvaluationLocation(EvaluationPass.InitialProperties, "1", "myFile", 42, element),
                    new ProfiledLocation(TimeSpan.MaxValue, TimeSpan.MinValue, 2)}
            }) };
            
        }

        /// <summary>
        /// Create a derived class so that we can test the default constructor in order to increase code coverage and 
        /// verify this code path does not cause any exceptions.
        /// </summary>
        private class ProjectFinishedEventArgs2 : ProjectFinishedEventArgs
        {
            /// <summary>
            /// Default constructor
            /// </summary>
            public ProjectFinishedEventArgs2()
                : base()
            {
            }
        }
    }
}
