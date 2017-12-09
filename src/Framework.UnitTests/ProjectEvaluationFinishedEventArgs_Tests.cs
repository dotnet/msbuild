// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Unit tests for ProjectEvaluationFinishedEventArgs</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework.Profiler;
using Microsoft.Build.UnitTests.BackEnd;
using Xunit;

namespace Microsoft.Build.Framework.UnitTests
{
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
    }
}
