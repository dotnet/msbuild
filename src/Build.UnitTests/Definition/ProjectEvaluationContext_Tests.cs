// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>

using System.Collections.Generic;
using System.IO;
using System.Xml;
using Microsoft.Build.BackEnd.SdkResolution;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Framework;
using Microsoft.Build.Unittest;
using Shouldly;
using Xunit;
using SdkResult = Microsoft.Build.BackEnd.SdkResolution.SdkResult;

namespace Microsoft.Build.UnitTests.Definition
{
    /// <summary>
    /// Tests some manipulations of Project and ProjectCollection that require dealing with internal data. 
    /// </summary>
    public class ProjectEvaluationContext_Tests
    {
        [Fact]
        public void ContextSdkResolverIsUsed()
        {
            using (var env = TestEnvironment.Create())
            {
                var resolver = new ConfigurableMockSdkResolver(
                    new Dictionary<string, SdkResult>
                    {
                        {"foo", new SdkResult(new SdkReference("foo", "1.0.0", null), "path", "1.0.0", null)},
                        {"bar", new SdkResult(new SdkReference("bar", "1.0.0", null), "path", "1.0.0", null)}
                    });

                var context = CreateMockContextWithResolver(resolver);

                var collection = env.CreateProjectCollection().Collection;

                var project1 = Project.FromXmlReader(XmlReader.Create(new StringReader("<Project Sdk=\"foo\"></Project>")), new ProjectOptions
                {
                    ProjectCollection = collection,
                    EvaluationContext = context,
                    LoadSettings = ProjectLoadSettings.IgnoreMissingImports
                });

                var project2 = Project.FromXmlReader(XmlReader.Create(new StringReader("<Project Sdk=\"bar\"></Project>")), new ProjectOptions
                {
                    ProjectCollection = collection,
                    EvaluationContext = context,
                    LoadSettings = ProjectLoadSettings.IgnoreMissingImports
                });

                var project3 = Project.FromXmlReader(XmlReader.Create(new StringReader("<Project Sdk=\"foo\"></Project>")), new ProjectOptions
                {
                    ProjectCollection = collection,
                    EvaluationContext = context,
                    LoadSettings = ProjectLoadSettings.IgnoreMissingImports
                });

                // results are cached, so each sdk is resolved once
                resolver.ResolvedCalls.Count.ShouldBe(2);
                resolver.ResolvedCalls["foo"].ShouldBe(1);
                resolver.ResolvedCalls["bar"].ShouldBe(1);
            }
        }

        private static EvaluationContextBase CreateMockContextWithResolver(ConfigurableMockSdkResolver resolver)
        {
            var context = (EvaluationContextBase)Project.EvaluationContextFactory.CreateContext();
            var cachingSdkService = (SdkResolverCachingWrapper)context.SdkResolverService;
            var sdkService = (SdkResolverService)cachingSdkService.TestOnlyGetWrappedService;

            sdkService.InitializeForTests(null, new List<SdkResolver> { resolver });

            return context;
        }
    }
}
