// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Xunit;
using Microsoft.Extensions.Testing.Abstractions;
using System.Reflection;
using FluentAssertions;
using TestAppWithPortablePdbs;

namespace Microsoft.Extensions.Testing.Abstractions.Tests
{
    public class GivenThatIWantToUsePortablePdbsToFindMethodInformation
    {
        private PortablePdbReader _pdbReader;

        public GivenThatIWantToUsePortablePdbsToFindMethodInformation()
        {
            var stream = new FileStream(
                Path.Combine(AppContext.BaseDirectory, "TestAppWithPortablePdbs.pdb"),
                FileMode.Open,
                FileAccess.Read);
            _pdbReader = new PortablePdbReader(stream);
        }

        [Fact]
        public void It_returns_the_right_file_and_the_right_file_number_when_the_method_exists_in_the_pdb()
        {
            var type = typeof (ClassForPortablePdbs);
            var methodInfo = type.GetMethod("TestMethodForPortablePdbs");

            var sourceInformation = _pdbReader.GetSourceInformation(methodInfo);

            sourceInformation.Should().NotBeNull();
            sourceInformation.Filename.Should().Contain("ClassForPortablePdbs.cs");
            sourceInformation.LineNumber.Should().Be(6);
        }

        [Fact]
        public void It_returns_null_when_MethodInfo_is_null()
        {
            var type = typeof(ClassForPortablePdbs);
            var methodInfo = type.GetMethod("Name_of_a_test_that_does_not_exist");

            var sourceInformation = _pdbReader.GetSourceInformation(methodInfo);

            sourceInformation.Should().BeNull();
        }

        [Fact]
        public void It_returns_null_when_the_method_does_not_exist_in_the_pdb()
        {
            var type = typeof(PortablePdbReader);
            var methodInfo = type.GetMethod("GetSourceInformation");

            var sourceInformation = _pdbReader.GetSourceInformation(methodInfo);

            sourceInformation.Should().BeNull();
        }

        [Fact]
        public void It_allows_us_to_invoke_GetSourceInformation_multiple_times()
        {
            var type = typeof(ClassForPortablePdbs);
            var firstMethodInfo = type.GetMethod("TestMethodForPortablePdbs");
            var secondMethodInfo = type.GetMethod("AnotherTestMethodForPortablePdbs");

            var sourceInformation = _pdbReader.GetSourceInformation(secondMethodInfo);
            sourceInformation.Should().NotBeNull();
            sourceInformation.Filename.Should().Contain("ClassForPortablePdbs.cs");
            sourceInformation.LineNumber.Should().Be(10);

            sourceInformation = _pdbReader.GetSourceInformation(firstMethodInfo);
            sourceInformation.Should().NotBeNull();
            sourceInformation.Filename.Should().Contain("ClassForPortablePdbs.cs");
            sourceInformation.LineNumber.Should().Be(6);

            sourceInformation = _pdbReader.GetSourceInformation(secondMethodInfo);
            sourceInformation.Should().NotBeNull();
            sourceInformation.Filename.Should().Contain("ClassForPortablePdbs.cs");
            sourceInformation.LineNumber.Should().Be(10);
        }
    }
}
