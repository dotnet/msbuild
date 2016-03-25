// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using System;
using System.IO;
using System.Reflection;
using TestAppWithFullPdbs;
using Xunit;

namespace Microsoft.Extensions.Testing.Abstractions.Tests
{
    public class GivenThatIWantToUseFullPdbsToFindMethodInformation
    {
        public FullPdbReader _pdbReader;

        public GivenThatIWantToUseFullPdbsToFindMethodInformation()
        {
            var stream = new FileStream(
                Path.Combine(AppContext.BaseDirectory, "TestAppWithFullPdbs.pdb"),
                FileMode.Open,
                FileAccess.Read);
            _pdbReader = new FullPdbReader(stream);
        }

        [WindowsOnlyFact]
        public void It_returns_the_right_file_and_the_right_file_number_when_the_method_exists_in_the_pdb()
        {
            var type = typeof(ClassForFullPdbs);
            var methodInfo = type.GetMethod("TestMethodForFullPdbs");

            var sourceInformation = _pdbReader.GetSourceInformation(methodInfo);

            sourceInformation.Should().NotBeNull();
            sourceInformation.Filename.Should().Contain("ClassForFullPdbs.cs");
            sourceInformation.LineNumber.Should().Be(6);
        }

        [WindowsOnlyFact]
        public void It_returns_null_when_MethodInfo_is_null()
        {
            var type = typeof(ClassForFullPdbs);
            var methodInfo = type.GetMethod("Name_of_a_test_that_does_not_exist");

            var sourceInformation = _pdbReader.GetSourceInformation(methodInfo);

            sourceInformation.Should().BeNull();
        }

        [WindowsOnlyFact]
        public void It_returns_null_when_the_method_does_not_exist_in_the_pdb()
        {
            var type = typeof(PortablePdbReader);
            var methodInfo = type.GetMethod("GetSourceInformation");

            var sourceInformation = _pdbReader.GetSourceInformation(methodInfo);

            sourceInformation.Should().BeNull();
        }

        [WindowsOnlyFact]
        public void It_allows_us_to_invoke_GetSourceInformation_multiple_times()
        {
            var type = typeof(ClassForFullPdbs);
            var firstMethodInfo = type.GetMethod("TestMethodForFullPdbs");
            var secondMethodInfo = type.GetMethod("AnotherTestMethodForFullPdbs");

            var sourceInformation = _pdbReader.GetSourceInformation(secondMethodInfo);
            sourceInformation.Should().NotBeNull();
            sourceInformation.Filename.Should().Contain("ClassForFullPdbs.cs");
            sourceInformation.LineNumber.Should().Be(10);

            sourceInformation = _pdbReader.GetSourceInformation(firstMethodInfo);
            sourceInformation.Should().NotBeNull();
            sourceInformation.Filename.Should().Contain("ClassForFullPdbs.cs");
            sourceInformation.LineNumber.Should().Be(6);

            sourceInformation = _pdbReader.GetSourceInformation(secondMethodInfo);
            sourceInformation.Should().NotBeNull();
            sourceInformation.Filename.Should().Contain("ClassForFullPdbs.cs");
            sourceInformation.LineNumber.Should().Be(10);
        }
    }
}
