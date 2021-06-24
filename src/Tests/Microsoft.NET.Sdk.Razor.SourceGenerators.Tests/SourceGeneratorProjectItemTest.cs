// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;
using Moq;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;

namespace Microsoft.NET.Sdk.Razor.SourceGenerators
{
    public class SourceGeneratorProjectItemTest
    {
        [Fact]
        public void PhysicalPath_ReturnsSourceTextPath()
        {
            // Arrange
            var emptyBasePath = "/";
            var path = "/foo/bar.cshtml";
            var projectItem = new SourceGeneratorProjectItem(
                filePath: path,
                basePath: emptyBasePath,
                relativePhysicalPath: "/foo",
                fileKind: FileKinds.Legacy,
                additionalText: new TestAdditionalText(string.Empty),
                cssScope: null);

            // Act
            var physicalPath = projectItem.PhysicalPath;

            // Assert
            Assert.Equal("dummy", physicalPath);
        }

        [Theory]
        [InlineData("/Home/Index")]
        [InlineData("EditUser")]
        public void Extension_ReturnsNullIfFileDoesNotHaveExtension(string path)
        {
            // Arrange
            var projectItem = new SourceGeneratorProjectItem(
                filePath: path,
                basePath: "/views",
                relativePhysicalPath: "/foo",
                fileKind: FileKinds.Legacy,
                additionalText: new TestAdditionalText(string.Empty),
                cssScope: null);

            // Act
            var extension = projectItem.Extension;

            // Assert
            Assert.Null(extension);
        }

        [Theory]
        [InlineData("/Home/Index.cshtml", ".cshtml")]
        [InlineData("/Home/Index.en-gb.cshtml", ".cshtml")]
        [InlineData("EditUser.razor", ".razor")]
        public void Extension_ReturnsFileExtension(string path, string expected)
        {
            // Arrange
            var projectItem = new SourceGeneratorProjectItem(
                filePath: path,
                basePath: "/views",
                relativePhysicalPath: "/foo",
                fileKind: FileKinds.Legacy,
                additionalText: new TestAdditionalText(string.Empty),
                cssScope: null);

            // Act
            var extension = projectItem.Extension;

            // Assert
            Assert.Equal(expected, extension);
        }

        [Theory]
        [InlineData("Home/Index.cshtml", "Index.cshtml")]
        [InlineData("/Accounts/Customers/Manage-en-us.razor", "Manage-en-us.razor")]
        public void FileName_ReturnsFileNameWithExtension(string path, string expected)
        {
            // Arrange
            var projectItem = new SourceGeneratorProjectItem(
                filePath: path,
                basePath: "/",
                relativePhysicalPath: "/foo",
                fileKind: FileKinds.Legacy,
                additionalText: new TestAdditionalText(string.Empty),
                cssScope: null);

            // Act
            var fileName = projectItem.FileName;

            // Assert
            Assert.Equal(expected, fileName);
        }

        [Theory]
        [InlineData("Home/Index", "Home/Index")]
        [InlineData("Home/Index.cshtml", "Home/Index")]
        [InlineData("/Accounts/Customers/Manage.en-us.razor", "/Accounts/Customers/Manage.en-us")]
        [InlineData("/Accounts/Customers/Manage-en-us.razor", "/Accounts/Customers/Manage-en-us")]
        public void PathWithoutExtension_ExcludesExtension(string path, string expected)
        {
            // Arrange
            var projectItem = new SourceGeneratorProjectItem(
                filePath: path,
                basePath: "/",
                relativePhysicalPath: "/foo",
                fileKind: FileKinds.Legacy,
                additionalText: new TestAdditionalText(string.Empty),
                cssScope: null);

            // Act
            var fileName = projectItem.FilePathWithoutExtension;

            // Assert
            Assert.Equal(expected, fileName);
        }
    }
}
