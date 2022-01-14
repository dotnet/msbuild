// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.InteropServices;
using Xunit;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.NET.Sdk.Razor.SourceGenerators
{
    public class SourceTextSourceLineCollectionTest
    {
        [Fact]
        public void GetLocation_Negative()
        {
            // Arrange
            var content = @"@addTagHelper, * Stuff
@* A comment *@";
            var sourceText = SourceText.From(content);
            var collection = new SourceTextSourceLineCollection("dummy", sourceText.Lines);

            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => collection.GetLocation(-1));
        }

        [Fact]
        public void GetLocation_TooBig()
        {
            // Arrange
            var content = @"addTagHelper, * Stuff
@* A comment *@";
            var sourceText = SourceText.From(content);
            var collection = new SourceTextSourceLineCollection("dummy", sourceText.Lines);

            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => collection.GetLocation(40));
        }

        [Fact]
        public void GetLocation_AtStart()
        {
            // Arrange
            var content = @"@addTaghelper, * Stuff
@* A comment *@";
            var sourceText = SourceText.From(content);
            var collection = new SourceTextSourceLineCollection("dummy", sourceText.Lines);

            // Act
            var location = collection.GetLocation(0);

            // Assert
            var expected = new SourceLocation("dummy", 0, 0, 0);
            Assert.Equal(expected, location);
        }

        [Fact]
        public void GetLocation_AtEnd()
        {
            // Arrange
            var content = @"@addTagHelper, * Stuff
@* A comment *@";
            var sourceText = SourceText.From(content);
            var collection = new SourceTextSourceLineCollection("dummy", sourceText.Lines);
            var length = content.Length;

            // Act
            var location = collection.GetLocation(length);

            // Assert
            var expected = new SourceLocation("dummy", length, 1, 15);
            Assert.Equal(expected, location);
        }

        [Fact]
        public void GetLineLength_Negative()
        {
            // Arrange
            var content = @"@addTagHelper, * Stuff
@* A comment *@";
            var sourceText = SourceText.From(content);
            var collection = new SourceTextSourceLineCollection("dummy", sourceText.Lines);

            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => collection.GetLineLength(-1));
        }

        [Fact]
        public void GetLineLength_Bigger()
        {
            // Arrange
            var content = @"@addTagHelper, * Stuff
@* A comment *@";
            var sourceText = SourceText.From(content);
            var collection = new SourceTextSourceLineCollection("dummy", sourceText.Lines);

            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => collection.GetLineLength(40));
        }

        [Fact]
        public void GetLineLength_AtStart()
        {
            // Arrange
            var content = @"@addTagHelper, * Stuff
@* A comment *@";
            var sourceText = SourceText.From(content);
            var collection = new SourceTextSourceLineCollection("dummy", sourceText.Lines);

            // Act
            var lineLength = collection.GetLineLength(0);

            // Assert
            var expectedLineLength = 22 + Environment.NewLine.Length;
            Assert.Equal(expectedLineLength, lineLength);
        }

        [Fact]
        public void GetLineLength_AtEnd()
        {
            // Arrange
            var content = @"@addTagHelper, * Stuff
@* A comment *@";
            var sourceText = SourceText.From(content);
            var collection = new SourceTextSourceLineCollection("dummy", sourceText.Lines);

            // Act
            var lineLength = collection.GetLineLength(1);

            // Assert
            var expectedLineLength = 15;
            Assert.Equal(expectedLineLength, lineLength);
        }
    }
}
