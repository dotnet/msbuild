// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Experimental.BuildCheck.Analyzers;
using Shouldly;
using Xunit;

namespace Microsoft.Build.BuildCheck.UnitTests
{
    public class SharedOutputPathAnalyzer_Tests
    {
        private readonly SharedOutputPathAnalyzer _analyzer;

        private readonly MockBuildCheckRegistrationContext _registrationContext;

        public SharedOutputPathAnalyzer_Tests()
        {
            _analyzer = new SharedOutputPathAnalyzer();
            _registrationContext = new MockBuildCheckRegistrationContext();
            _analyzer.RegisterActions(_registrationContext);
        }

        private EvaluatedPropertiesAnalysisData MakeEvaluatedPropertiesAction(
            string projectFile,
            Dictionary<string, string>? evaluatedProperties,
            IReadOnlyDictionary<string, (string EnvVarValue, string File, int Line, int Column)>? evaluatedEnvVars)
        {
            return new EvaluatedPropertiesAnalysisData(
                projectFile,
                null,
                evaluatedProperties ?? new Dictionary<string, string>(),
                evaluatedEnvVars ?? new Dictionary<string, (string EnvVarValue, string File, int Line, int Column)>());
        }

        [Fact]
        public void TestTwoProjectsWithSameRelativeOutputPath()
        {
            // Full output and intermediate paths are different: "C:/fake1/bin/Debug" and "C:/fake1/obj/Debug".
            string projectFile1 = NativeMethodsShared.IsWindows ? "C:\\fake1\\project1.proj" : "/fake1/project1.proj";
            _registrationContext.TriggerEvaluatedPropertiesAction(MakeEvaluatedPropertiesAction(
                projectFile1,
                new Dictionary<string, string> {
                    { "OutputPath", "bin/Debug" },
                    { "IntermediateOutputPath", "obj/Debug" },
                },
                null));

            // Full output and intermediate paths are different: "C:/fake2/bin/Debug" and "C:/fake2/obj/Debug".
            string projectFile2 = NativeMethodsShared.IsWindows ? "C:\\fake2\\project2.proj" : "/fake2/project2.proj";
            _registrationContext.TriggerEvaluatedPropertiesAction(MakeEvaluatedPropertiesAction(
                projectFile2,
                new Dictionary<string, string> {
                    { "OutputPath", "bin/Debug" },
                    { "IntermediateOutputPath", "obj/Debug" },
                },
                null));

            // Relative paths coincide but full does not. SharedOutputPathAnalyzer should not report it.
            _registrationContext.Results.Count.ShouldBe(0);
        }

        [Fact]
        public void TestProjectsWithDifferentPathsSeparators()
        {
            // Paths separators are messed up.
            string projectFile1 = NativeMethodsShared.IsWindows ? "C:\\fake\\project1.proj" : "/fake/project1.proj";
            string projectFile2 = NativeMethodsShared.IsWindows ? "C:\\fake\\project2.proj" : "/fake/project2.proj";

            _registrationContext.TriggerEvaluatedPropertiesAction(MakeEvaluatedPropertiesAction(
                projectFile1,
                new Dictionary<string, string> {
                    { "OutputPath", "bin/Debug" },
                    { "IntermediateOutputPath", "obj\\Debug" },
                },
                null));

            _registrationContext.TriggerEvaluatedPropertiesAction(MakeEvaluatedPropertiesAction(
                projectFile2,
                new Dictionary<string, string> {
                    { "OutputPath", "bin/Debug" },
                    { "IntermediateOutputPath", "obj\\Debug" },
                },
                null));

            // 2 reports for bin and obj folders.
            _registrationContext.Results.Count.ShouldBe(2);
            _registrationContext.Results[0].BuildAnalyzerRule.Id.ShouldBe("BC0101");
            _registrationContext.Results[1].BuildAnalyzerRule.Id.ShouldBe("BC0101");

            // Check that paths are formed with correct paths separators
            string wrongPathSeparator = NativeMethodsShared.IsWindows ? "/" : "\\";

            foreach (string path in _registrationContext.Results[0].MessageArgs)
            {
                path.ShouldNotContain(wrongPathSeparator);
            }
            foreach (string path in _registrationContext.Results[1].MessageArgs)
            {
                path.ShouldNotContain(wrongPathSeparator);
            }
        }

        [Fact]
        public void TestThreeProjectsWithSameOutputPath()
        {
            string projectFolder = NativeMethodsShared.IsWindows ? "C:\\fake\\" : "/fake/";
            string projectFile1 = $"{projectFolder}project1.proj";
            string projectFile2 = $"{projectFolder}project2.proj";
            string projectFile3 = $"{projectFolder}project3.proj";
            var evaluatedProperties = new Dictionary<string, string> {
                    { "OutputPath", "bin/Debug" },
                    { "IntermediateOutputPath", "obj\\Debug" },};

            _registrationContext.TriggerEvaluatedPropertiesAction(MakeEvaluatedPropertiesAction(
                projectFile1,
                evaluatedProperties,
                null));

            _registrationContext.TriggerEvaluatedPropertiesAction(MakeEvaluatedPropertiesAction(
                projectFile2,
                evaluatedProperties,
                null));

            _registrationContext.TriggerEvaluatedPropertiesAction(MakeEvaluatedPropertiesAction(
                projectFile3,
                evaluatedProperties,
                null));

            _registrationContext.Results.Count.ShouldBe(4); // 4 reports for two pairs of project: (1, 2) and (1, 3).
            foreach (var result in _registrationContext.Results)
            {
                result.BuildAnalyzerRule.Id.ShouldBe("BC0101");
            }
        }
    }
}
