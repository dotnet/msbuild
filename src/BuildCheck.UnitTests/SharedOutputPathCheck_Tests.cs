// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Experimental.BuildCheck.Checks;
using Shouldly;
using Xunit;

namespace Microsoft.Build.BuildCheck.UnitTests
{
    public class SharedOutputPathCheck_Tests
    {
        private readonly SharedOutputPathCheck _check;

        private readonly MockBuildCheckRegistrationContext _registrationContext;

        public SharedOutputPathCheck_Tests()
        {
            _check = new SharedOutputPathCheck();
            _registrationContext = new MockBuildCheckRegistrationContext();
            _check.RegisterActions(_registrationContext);
        }

        private EvaluatedPropertiesCheckData MakeEvaluatedPropertiesAction(
            string projectFile,
            Dictionary<string, string>? evaluatedProperties,
            IReadOnlyDictionary<string, (string EnvVarValue, string File, int Line, int Column)>? evaluatedEnvVars)
        {
            return new EvaluatedPropertiesCheckData(
                projectFile,
                null,
                evaluatedProperties ?? new Dictionary<string, string>(),
                new Dictionary<string, string>());
        }

        [Fact]
        public void TestTwoProjectsWithSameRelativeOutputPath()
        {
            // Full output and intermediate paths are different: "C:/fake1/bin/Debug" and "C:/fake1/obj/Debug".
            string projectFile1 = Framework.NativeMethods.IsWindows ? "C:\\fake1\\project1.proj" : "/fake1/project1.proj";
            _registrationContext.TriggerEvaluatedPropertiesAction(MakeEvaluatedPropertiesAction(
                projectFile1,
                new Dictionary<string, string> {
                    { "OutputPath", "bin/Debug" },
                    { "IntermediateOutputPath", "obj/Debug" },
                },
                null));

            // Full output and intermediate paths are different: "C:/fake2/bin/Debug" and "C:/fake2/obj/Debug".
            string projectFile2 = Framework.NativeMethods.IsWindows ? "C:\\fake2\\project2.proj" : "/fake2/project2.proj";
            _registrationContext.TriggerEvaluatedPropertiesAction(MakeEvaluatedPropertiesAction(
                projectFile2,
                new Dictionary<string, string> {
                    { "OutputPath", "bin/Debug" },
                    { "IntermediateOutputPath", "obj/Debug" },
                },
                null));

            // Relative paths coincide but full does not. SharedOutputPathCheck should not report it.
            _registrationContext.Results.Count.ShouldBe(0);
        }

        [Fact]
        public void TestProjectsWithDifferentPathsSeparators()
        {
            // Paths separators are messed up.
            string projectFile1 = Framework.NativeMethods.IsWindows ? "C:\\fake\\project1.proj" : "/fake/project1.proj";
            string projectFile2 = Framework.NativeMethods.IsWindows ? "C:\\fake\\project2.proj" : "/fake/project2.proj";

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
            _registrationContext.Results[0].CheckRule.Id.ShouldBe("BC0101");
            _registrationContext.Results[1].CheckRule.Id.ShouldBe("BC0101");

            // Check that paths are formed with correct paths separators
            string wrongPathSeparator = Framework.NativeMethods.IsWindows ? "/" : "\\";

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
            string projectFolder = Framework.NativeMethods.IsWindows ? "C:\\fake\\" : "/fake/";
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
                result.CheckRule.Id.ShouldBe("BC0101");
            }
        }
    }
}
