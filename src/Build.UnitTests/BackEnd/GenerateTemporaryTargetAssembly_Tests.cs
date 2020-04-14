// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using Microsoft.Build.UnitTests;
using Shouldly;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Microsoft.Build.Engine.UnitTests.BackEnd
{
    sealed public class GenerateTemporaryTargetAssembly_Tests
    {
        [Fact]
        public void FailsWithOnlyTargetErrors()
        {
            using (TestEnvironment testenv = TestEnvironment.Create())
            {
                TransientTestFile otherproj = testenv.CreateFile("otherproj.csproj", @"
<Project>
    <Target Name=""ErrorTask"">
        <Error Text=""Task successfully failed."" />
    </Target>
</Project>");
                MockLogger logger = ObjectModelHelpers.BuildProjectExpectFailure(@$"
<Project>
    <UsingTask TaskName=""FailingBuilderTask"" AssemblyFile=""{Assembly.GetExecutingAssembly().Location}"" />
    <Target Name=""MyTarget"">
        <FailingBuilderTask CurrentProject=""{otherproj.Path}"" />
    </Target>
</Project>");
                logger.ErrorCount.ShouldBe(1);
                logger.Errors.First().Message.ShouldBe("Task successfully failed.");
            }
        }
    }
}
