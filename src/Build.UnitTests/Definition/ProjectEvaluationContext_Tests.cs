// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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
    ///     Tests some manipulations of Project and ProjectCollection that require dealing with internal data.
    /// </summary>
    public class ProjectEvaluationContext_Tests : IDisposable
    {
        public ProjectEvaluationContext_Tests()
        {
            _env = TestEnvironment.Create();

            _resolver = new SdkUtilities.ConfigurableMockSdkResolver(
                new Dictionary<string, SdkResult>
                {
                    {"foo", new SdkResult(new SdkReference("foo", "1.0.0", null), "path", "1.0.0", null)},
                    {"bar", new SdkResult(new SdkReference("bar", "1.0.0", null), "path", "1.0.0", null)}
                });
        }

        public void Dispose()
        {
            _env.Dispose();
        }

        private readonly SdkUtilities.ConfigurableMockSdkResolver _resolver;
        private readonly TestEnvironment _env;

        private static void SetResolverForContext(EvaluationContext context, SdkResolver resolver)
        {
            var sdkService = (SdkResolverService) context.SdkResolverService;

            sdkService.InitializeForTests(null, new List<SdkResolver> {resolver});
        }

        [Theory]
        [InlineData(EvaluationContext.SharingPolicy.Shared, 1, 1)]
        [InlineData(EvaluationContext.SharingPolicy.Isolated, 3, 3)]
        public void ContextSdkResolverIsUsed(EvaluationContext.SharingPolicy policy, int sdkLookupsForFoo, int sdkLookupsForBar)
        {
            try
            {
                EvaluationContext.TestOnlyAlterStateOnCreate = c => SetResolverForContext(c, _resolver);

                var context = EvaluationContext.Create(policy);
                EvaluateProjects(context);

                _resolver.ResolvedCalls["foo"].ShouldBe(sdkLookupsForFoo);
                _resolver.ResolvedCalls["bar"].ShouldBe(sdkLookupsForBar);
                _resolver.ResolvedCalls.Count.ShouldBe(2);
            }
            finally
            {
                EvaluationContext.TestOnlyAlterStateOnCreate = null;
            }
        }

        [Fact]
        public void DefaultContextIsIsolatedContext()
        {
            var contextHashcodesSeen = new HashSet<int>();

            EvaluateProjects(
                null,
                p =>
                {
                    var fieldInfo = p.GetType().GetField("_lastEvaluationContext", BindingFlags.NonPublic | BindingFlags.Instance);
                    var obj = fieldInfo.GetValue(p);

                    obj.ShouldBeOfType<EvaluationContext>();

                    var context = (EvaluationContext) obj;

                    context.Policy.ShouldBe(EvaluationContext.SharingPolicy.Isolated);

                    contextHashcodesSeen.ShouldNotContain(context.GetHashCode());

                    contextHashcodesSeen.Add(context.GetHashCode());
                });
        }

        private void EvaluateProjects(EvaluationContext context, Action<Project> projectAction = null)
        {
            var collection = _env.CreateProjectCollection().Collection;

            var project1 = Project.FromXmlReader(
                XmlReader.Create(new StringReader("<Project Sdk=\"foo\"></Project>")),
                new ProjectOptions
                {
                    ProjectCollection = collection,
                    EvaluationContext = context,
                    LoadSettings = ProjectLoadSettings.IgnoreMissingImports
                });

            projectAction?.Invoke(project1);

            var project2 = Project.FromXmlReader(
                XmlReader.Create(new StringReader("<Project Sdk=\"bar\"></Project>")),
                new ProjectOptions
                {
                    ProjectCollection = collection,
                    EvaluationContext = context,
                    LoadSettings = ProjectLoadSettings.IgnoreMissingImports
                });

            projectAction?.Invoke(project2);

            var project3 = Project.FromXmlReader(
                XmlReader.Create(new StringReader("<Project Sdk=\"foo\"></Project>")),
                new ProjectOptions
                {
                    ProjectCollection = collection,
                    EvaluationContext = context,
                    LoadSettings = ProjectLoadSettings.IgnoreMissingImports
                });

            projectAction?.Invoke(project3);

            var project4 = Project.FromXmlReader(
                XmlReader.Create(new StringReader("<Project Sdk=\"bar\"></Project>")),
                new ProjectOptions
                {
                    ProjectCollection = collection,
                    EvaluationContext = context,
                    LoadSettings = ProjectLoadSettings.IgnoreMissingImports
                });

            projectAction?.Invoke(project4);

            project3.AddItem("a", "b");
            project3.ReevaluateIfNecessary(context);

            projectAction?.Invoke(project3);

            project4.AddItem("a", "b");
            project4.ReevaluateIfNecessary(context);

            projectAction?.Invoke(project4);
        }
  }
}
