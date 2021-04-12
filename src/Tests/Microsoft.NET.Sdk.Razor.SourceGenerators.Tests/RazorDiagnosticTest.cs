// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Microsoft.NET.Sdk.Razor.SourceGenerators
{
    public class RazorDiagnosticTest
    {
        [Fact]
        public void AsDiagnostic_WithUndefinedSpanWorks()
        {
            // Arrange
            var diagnostics = RazorDiagnostic.Create(new RazorDiagnosticDescriptor("RZC1001", () => "Some message", RazorDiagnosticSeverity.Error), SourceSpan.Undefined);

            // Act
            var csharpDiagnostic = diagnostics.AsDiagnostic();

            // Assert
            Assert.Equal("Some message", csharpDiagnostic.GetMessage());
            Assert.Equal("RZC1001", csharpDiagnostic.Descriptor.Id);
            Assert.Equal(DiagnosticSeverity.Error, csharpDiagnostic.Severity);
            Assert.Equal(Location.None, csharpDiagnostic.Location);
        }

        [Fact]
        public void AsDiagnostic_WithSpanWorks()
        {
            // Arrange
            var span = new SourceSpan("some-file", 100, 1, 5, 10);
            var diagnostics = RazorDiagnostic.Create(new RazorDiagnosticDescriptor("RZC1001", () => "Some message", RazorDiagnosticSeverity.Error), span);

            // Act
            var csharpDiagnostic = diagnostics.AsDiagnostic();

            // Assert
            Assert.Equal("Some message", csharpDiagnostic.GetMessage());
            Assert.Equal("RZC1001", csharpDiagnostic.Descriptor.Id);
            Assert.Equal(DiagnosticSeverity.Error, csharpDiagnostic.Severity);
            Assert.Equal(100, csharpDiagnostic.Location.SourceSpan.Start);

            var lineSpan = csharpDiagnostic.Location.GetLineSpan();
            Assert.Equal("some-file", lineSpan.Path);
            Assert.Equal(1, lineSpan.StartLinePosition.Line);
            Assert.Equal(5, lineSpan.StartLinePosition.Character);
            Assert.Equal(1, lineSpan.EndLinePosition.Line);
            Assert.Equal(15, lineSpan.EndLinePosition.Character);
        }
    }
}
