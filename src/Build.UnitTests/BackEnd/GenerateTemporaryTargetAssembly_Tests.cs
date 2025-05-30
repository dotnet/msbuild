// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using System.Reflection;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.Engine.UnitTests.BackEnd
{
    public sealed class GenerateTemporaryTargetAssembly_Tests
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
