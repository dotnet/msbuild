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
        [InlineData(EvaluationContext.SharingPolicy.Shared)]
        [InlineData(EvaluationContext.SharingPolicy.Isolated)]
        public void ReevaluationShouldRespectContextLifetime(EvaluationContext.SharingPolicy policy)
        {
            var collection = _env.CreateProjectCollection().Collection;

            var context1 = EvaluationContext.Create(policy);

            var project = Project.FromXmlReader(
                XmlReader.Create(new StringReader("<Project></Project>")),
                new ProjectOptions
                {
                    ProjectCollection = collection,
                    EvaluationContext = context1,
                    LoadSettings = ProjectLoadSettings.IgnoreMissingImports
                });

            project.AddItem("a", "b");

            project.ReevaluateIfNecessary();

            var context2 = GetEvaluationContext(project);

            switch (policy)
            {
                case EvaluationContext.SharingPolicy.Shared:
                    context1.ShouldBeSameAs(context2);
                    break;
                case EvaluationContext.SharingPolicy.Isolated:
                    context1.ShouldNotBeSameAs(context2);
                    break;
            }
        }

        private static EvaluationContext GetEvaluationContext(Project p)
        {
            var fieldInfo = p.GetType().GetField("_lastEvaluationContext", BindingFlags.NonPublic | BindingFlags.Instance);
            var value = fieldInfo.GetValue(p);

            value.ShouldBeOfType<EvaluationContext>();

            return (EvaluationContext) value;
        }

        private static string[] _sdkResolutionProjects =
        {
            "<Project Sdk=\"foo\"></Project>",
            "<Project Sdk=\"bar\"></Project>",
            "<Project Sdk=\"foo\"></Project>",
            "<Project Sdk=\"bar\"></Project>"
        };

        [Theory]
        [InlineData(EvaluationContext.SharingPolicy.Shared, 1, 1)]
        [InlineData(EvaluationContext.SharingPolicy.Isolated, 4, 4)]
        public void ContextPinsSdkResolverCache(EvaluationContext.SharingPolicy policy, int sdkLookupsForFoo, int sdkLookupsForBar)
        {
            try
            {
                EvaluationContext.TestOnlyAlterStateOnCreate = c => SetResolverForContext(c, _resolver);

                var context = EvaluationContext.Create(policy);
                EvaluateProjects(_sdkResolutionProjects, context, null);

                _resolver.ResolvedCalls.Count.ShouldBe(2);
                _resolver.ResolvedCalls["foo"].ShouldBe(sdkLookupsForFoo);
                _resolver.ResolvedCalls["bar"].ShouldBe(sdkLookupsForBar);
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
                _sdkResolutionProjects,
                null,
                p =>
                {
                    var context = GetEvaluationContext(p);

                    context.Policy.ShouldBe(EvaluationContext.SharingPolicy.Isolated);

                    contextHashcodesSeen.ShouldNotContain(context.GetHashCode());

                    contextHashcodesSeen.Add(context.GetHashCode());
                });
        }
        public static IEnumerable<object> ContextPinsGlobExpansionCacheData
        {
            get
            {
                yield return new object[]
                {
                    EvaluationContext.SharingPolicy.Shared,
                    new[]
                    {
                        new[] {"0.cs"},
                        new[] {"0.cs"},
                        new[] {"0.cs"},
                        new[] {"0.cs"}
                    }
                };

                yield return new object[]
                {
                    EvaluationContext.SharingPolicy.Isolated,
                    new[]
                    {
                        new[] {"0.cs"},
                        new[] {"0.cs", "1.cs"},
                        new[] {"0.cs", "1.cs", "2.cs"},
                        new[] {"0.cs", "1.cs", "2.cs", "3.cs"},
                    }
                };
            }
        }

        private static string[] _globProjects =
        {
            @"<Project>
                <ItemGroup>
                    <i Include=`**/*.cs` />
                </ItemGroup>
            </Project>",

            @"<Project>
                <ItemGroup>
                    <i Include=`**/*.cs` />
                </ItemGroup>
            </Project>",
        };

        [Theory]
        [MemberData(nameof(ContextPinsGlobExpansionCacheData))]
        public void ContextPinsGlobExpansionCache(EvaluationContext.SharingPolicy policy, string[][] expectedGlobExpansions)
        {
            var projectDirectory = _env.DefaultTestDirectory.FolderPath;

            _env.SetCurrentDirectory(projectDirectory);

            var context = EvaluationContext.Create(policy);

            var evaluationCount = 0;

            File.WriteAllText(Path.Combine(projectDirectory, $"{evaluationCount}.cs"), "");

            EvaluateProjects(
                _globProjects,
                context,
                project =>
                {
                    var expectedGlobExpansion = expectedGlobExpansions[evaluationCount];
                    evaluationCount++;

                    File.WriteAllText(Path.Combine(projectDirectory, $"{evaluationCount}.cs"), "");

                    ObjectModelHelpers.AssertItems(expectedGlobExpansion, project.GetItems("i"));
                }
                );
        }

        /// <summary>
        /// Should be at least two test projects to test cache visibility between projects
        /// </summary>
        private void EvaluateProjects(string[] projectContents, EvaluationContext context, Action<Project> projectAction)
        {
            var collection = _env.CreateProjectCollection().Collection;

            var projects = new List<Project>(projectContents.Length);

            foreach (var projectContent in projectContents)
            {
                var project = Project.FromXmlReader(
                    XmlReader.Create(new StringReader(projectContent.Cleanup())),
                    new ProjectOptions
                    {
                        ProjectCollection = collection,
                        EvaluationContext = context,
                        LoadSettings = ProjectLoadSettings.IgnoreMissingImports
                    });

                projectAction?.Invoke(project);

                projects.Add(project);
            }

            foreach (var project in projects)
            {
                project.AddItem("a", "b");
                project.ReevaluateIfNecessary(context);

                projectAction?.Invoke(project);
            }
        }
    }
}
