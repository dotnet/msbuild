// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework.Profiler;
using Microsoft.Build.UnitTests.BackEnd;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    //  Although this tests the ProfilerResult API from Microsoft.Build.Framework, it uses the
    //  construction APIs in Microsoft.Build in the test, so this test is in the Microsoft.Build tests
    public class ProjectEvaluationFinishedEventArgs_Tests 
    {
        /// <summary>
        /// Roundtrip serialization tests for <see cref="ProfilerResult"/>
        /// </summary>
        [MemberData(nameof(GetProfilerResults))]
        [Theory]
        public void ProfilerResultRoundTrip(ProfilerResult profilerResult)
        {
            var writeTranslator = TranslationHelpers.GetWriteTranslator();
            ProfilerResult deserializedResult;

            writeTranslator.TranslateDotNet(ref profilerResult);

            var readTranslator = TranslationHelpers.GetReadTranslator();

            readTranslator.TranslateDotNet(ref deserializedResult);

            Assert.Equal(deserializedResult, profilerResult);
        }

        public static IEnumerable<object[]> GetProfilerResults()
        {
            yield return new object[] { new ProfilerResult(new Dictionary<EvaluationLocation, ProfiledLocation>()) };

            yield return new object[] { new ProfilerResult(new Dictionary<EvaluationLocation, ProfiledLocation>
            {
                {new EvaluationLocation(0, null, EvaluationPass.TotalEvaluation, "1", "myFile", 42, "elementName", "description", EvaluationLocationKind.Condition), new ProfiledLocation(TimeSpan.MaxValue, TimeSpan.MinValue, 2)},
                {new EvaluationLocation(1, 0, EvaluationPass.Targets, "1", null, null, null, null, EvaluationLocationKind.Glob), new ProfiledLocation(TimeSpan.MaxValue, TimeSpan.MinValue, 2)},
                {new EvaluationLocation(2, 0, EvaluationPass.LazyItems, "2", null, null, null, null, EvaluationLocationKind.Element), new ProfiledLocation(TimeSpan.Zero, TimeSpan.Zero, 0)}
            }) };

            var element = new ProjectRootElement(
                XmlReader.Create(new MemoryStream(Encoding.UTF8.GetBytes(
                    "<Project xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\"/>"))),
                new ProjectRootElementCache(false), false, false);

            yield return new object[] { new ProfilerResult(new Dictionary<EvaluationLocation, ProfiledLocation>
            {
                {EvaluationLocation.CreateLocationForCondition(null, EvaluationPass.UsingTasks, "1", "myFile", 42, "conditionCase"), new ProfiledLocation(TimeSpan.MaxValue, TimeSpan.MinValue, 2)},
                {EvaluationLocation.CreateLocationForProject(null, EvaluationPass.InitialProperties, "1", "myFile", 42, element),
                    new ProfiledLocation(TimeSpan.MaxValue, TimeSpan.MinValue, 2)},
                {EvaluationLocation.CreateLocationForGlob(null, EvaluationPass.InitialProperties, "1", "myFile", 42, "glob description"),
                new ProfiledLocation(TimeSpan.MaxValue, TimeSpan.MinValue, 2)}
            }) };
        }
    }
}
