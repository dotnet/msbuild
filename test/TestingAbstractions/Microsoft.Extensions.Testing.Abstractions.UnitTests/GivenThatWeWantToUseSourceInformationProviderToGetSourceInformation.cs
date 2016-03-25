// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.Extensions.Testing.Abstractions;
using Moq;
using Xunit;
using System.Reflection;
using FluentAssertions;

namespace Microsoft.Extensions.Testing.Abstractions.UnitTests
{
    public class GivenThatWeWantToUseSourceInformationProviderToGetSourceInformation
    {
        private string _pdbPath = Path.Combine(AppContext.BaseDirectory, "TestAppWithPortablePdbs.pdb");

        [Fact]
        public void It_creates_a_pdb_reader_right_away()
        {
            var pdbReaderFactoryMock = new Mock<IPdbReaderFactory>();
            var sourceInformationProvider = new SourceInformationProvider(_pdbPath, null, pdbReaderFactoryMock.Object);

            pdbReaderFactoryMock.Verify(p => p.Create(_pdbPath), Times.Once);
        }

        [Fact]
        public void It_uses_the_reader_to_get_the_SourceInformation()
        {
            var type = typeof(GivenThatWeWantToUseSourceInformationProviderToGetSourceInformation);
            var methodInfo = type.GetMethod("It_uses_the_reader_to_get_the_SourceInformation");

            var expectedSourceInformation = new SourceInformation("some file path.cs", 12);
            var pdbReaderMock = new Mock<IPdbReader>();
            pdbReaderMock.Setup(p => p.GetSourceInformation(methodInfo)).Returns(expectedSourceInformation);

            var pdbReaderFactoryMock = new Mock<IPdbReaderFactory>();
            pdbReaderFactoryMock.Setup(p => p.Create(_pdbPath)).Returns(pdbReaderMock.Object);

            var sourceInformationProvider = new SourceInformationProvider(_pdbPath, null, pdbReaderFactoryMock.Object);

            var actualSourceInformation = sourceInformationProvider.GetSourceInformation(methodInfo);

            actualSourceInformation.Should().Be(expectedSourceInformation);
        }

        [Fact]
        public void It_disposes_of_the_reader_when_it_gets_disposed()
        {
            var pdbReaderMock = new Mock<IPdbReader>();

            var pdbReaderFactoryMock = new Mock<IPdbReaderFactory>();
            pdbReaderFactoryMock.Setup(p => p.Create(_pdbPath)).Returns(pdbReaderMock.Object);

            using (var sourceInformationProvider =
                new SourceInformationProvider(_pdbPath, null, pdbReaderFactoryMock.Object))
            {
            }

            pdbReaderMock.Verify(p => p.Dispose(), Times.Once);
        }
    }
}
