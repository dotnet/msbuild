// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Microsoft.NET.Sdk.Razor.SourceGenerators
{
    public class RazorSourceGenerationOptionsTest
    {
        [Fact]
        public void Equals_ReturnsFalse_IfConfigurationChanged()
        {
            // Arrange
            var options1 = new RazorSourceGenerationOptions
            {
                Configuration = RazorConfiguration.Default,
            };

            var options2 = new RazorSourceGenerationOptions
            {
                Configuration = RazorConfiguration.Create(RazorLanguageVersion.Latest, "3.1", Enumerable.Empty<RazorExtension>()),
            };

            // Act
            var equals = options1.Equals(options2);

            // Assert
            Assert.False(equals);
        }

        [Fact]
        public void Equals_ReturnsFalse_IfLanguageChanged()
        {
            // Arrange
            var options1 = new RazorSourceGenerationOptions
            {
                CSharpLanguageVersion = LanguageVersion.CSharp10,
                Configuration = RazorConfiguration.Default,
            };

            var options2 = new RazorSourceGenerationOptions
            {
                Configuration = RazorConfiguration.Default,
                CSharpLanguageVersion = LanguageVersion.CSharp9,
            };

            // Act
            var equals = options1.Equals(options2);

            // Assert
            Assert.False(equals);
        }

        [Fact]
        public void Equals_ReturnsFalse_IfGenerateMetadataSourceChecksumAttributesChanged()
        {
            // Arrange
            var options1 = new RazorSourceGenerationOptions
            {
                CSharpLanguageVersion = LanguageVersion.CSharp10,
                Configuration = RazorConfiguration.Default, 
                GenerateMetadataSourceChecksumAttributes = false,
            };

            var options2 = new RazorSourceGenerationOptions
            {
                Configuration = RazorConfiguration.Default,
                CSharpLanguageVersion = LanguageVersion.CSharp9,
                GenerateMetadataSourceChecksumAttributes = true,
            };

            // Act
            var equals = options1.Equals(options2);

            // Assert
            Assert.False(equals);
        }

        [Fact]
        public void Equals_ReturnsFalse_IfRootNamespaceChanged()
        {
            // Arrange
            var options1 = new RazorSourceGenerationOptions
            {
                CSharpLanguageVersion = LanguageVersion.Latest,
                Configuration = RazorConfiguration.Default,
                GenerateMetadataSourceChecksumAttributes = true,
                RootNamespace = "Initial",
            };

            var options2 = new RazorSourceGenerationOptions
            {
                Configuration = RazorConfiguration.Default,
                CSharpLanguageVersion = LanguageVersion.Latest,
                GenerateMetadataSourceChecksumAttributes = true,
                RootNamespace = "Different",
            };

            // Act
            var equals = options1.Equals(options2);

            // Assert
            Assert.False(equals);
        }

        [Fact]
        public void Equals_ReturnsFalse_IfSupportLocalizedComponentNameChanged()
        {
            // Arrange
            var options1 = new RazorSourceGenerationOptions
            {
                CSharpLanguageVersion = LanguageVersion.Latest,
                Configuration = RazorConfiguration.Default,
                GenerateMetadataSourceChecksumAttributes = true,
                RootNamespace = "Asp",
                SupportLocalizedComponentNames = false,
            };

            var options2 = new RazorSourceGenerationOptions
            {
                Configuration = RazorConfiguration.Default,
                CSharpLanguageVersion = LanguageVersion.Latest,
                GenerateMetadataSourceChecksumAttributes = true,
                RootNamespace = "Asp",
                SupportLocalizedComponentNames = true,
            };

            // Act
            var equals = options1.Equals(options2);

            // Assert
            Assert.False(equals);
        }

        [Fact]
        public void Equals_ReturnsFalse_IfSuppressRazorSourceGeneratorChanged()
        {
            // Arrange
            var options1 = new RazorSourceGenerationOptions
            {
                CSharpLanguageVersion = LanguageVersion.Latest,
                Configuration = RazorConfiguration.Default,
                GenerateMetadataSourceChecksumAttributes = true,
                RootNamespace = "Asp",
                SupportLocalizedComponentNames = true,
                SuppressRazorSourceGenerator = true,
            };

            var options2 = new RazorSourceGenerationOptions
            {
                Configuration = RazorConfiguration.Default,
                CSharpLanguageVersion = LanguageVersion.Latest,
                GenerateMetadataSourceChecksumAttributes = true,
                RootNamespace = "Asp",
                SupportLocalizedComponentNames = true,
                SuppressRazorSourceGenerator = false,
            };

            // Act
            var equals = options1.Equals(options2);

            // Assert
            Assert.False(equals);
        }

        [Fact]
        public void Equals_ReturnsTrue_IfValuesAreUnchanged()
        {
            // Arrange
            var options1 = new RazorSourceGenerationOptions
            {
                CSharpLanguageVersion = LanguageVersion.Latest,
                Configuration = RazorConfiguration.Default,
                GenerateMetadataSourceChecksumAttributes = true,
                RootNamespace = "Asp",
                SupportLocalizedComponentNames = true,
                SuppressRazorSourceGenerator = true,
            };

            var options2 = new RazorSourceGenerationOptions
            {
                Configuration = RazorConfiguration.Default,
                CSharpLanguageVersion = LanguageVersion.Latest,
                GenerateMetadataSourceChecksumAttributes = true,
                RootNamespace = "Asp",
                SupportLocalizedComponentNames = true,
                SuppressRazorSourceGenerator = true,
            };

            // Act
            var equals = options1.Equals(options2);

            // Assert
            Assert.True(equals);
        }

        [Fact]
        public void Equals_ReturnsTrue_IfRazorConfigurationAreDifferentInstancesButEqualValues()
        {
            // Arrange
            var options1 = new RazorSourceGenerationOptions
            {
                CSharpLanguageVersion = LanguageVersion.Latest,
                Configuration = RazorConfiguration.Create(RazorLanguageVersion.Parse("6.0"), "Default", Enumerable.Empty<RazorExtension>(), useConsolidatedMvcViews: true),
                GenerateMetadataSourceChecksumAttributes = true,
                RootNamespace = "Asp",
                SupportLocalizedComponentNames = true,
                SuppressRazorSourceGenerator = true,
            };

            var options2 = new RazorSourceGenerationOptions
            {
                Configuration = RazorConfiguration.Create(RazorLanguageVersion.Parse("6.0"), "Default", Enumerable.Empty<RazorExtension>(), useConsolidatedMvcViews: true),
                CSharpLanguageVersion = LanguageVersion.Latest,
                GenerateMetadataSourceChecksumAttributes = true,
                RootNamespace = "Asp",
                SupportLocalizedComponentNames = true,
                SuppressRazorSourceGenerator = true,
            };

            // Act
            var equals = options1.Equals(options2);

            // Assert
            Assert.True(equals);
        }
    }
}
