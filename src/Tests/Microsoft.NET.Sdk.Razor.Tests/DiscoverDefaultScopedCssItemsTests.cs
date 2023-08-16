// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Sdk.Razor.Test
{
    public class DiscoverDefaultScopedCssItemsTests
    {
        [Fact]
        public void DiscoversScopedCssFiles_BasedOnTheirExtension()
        {
            // Arrange
            var taskInstance = new DiscoverDefaultScopedCssItems()
            {
                Content = new[]
                {
                    new TaskItem("TestFiles/Pages/Counter.razor.css"),
                    new TaskItem("TestFiles/Pages/Index.razor.css"),
                    new TaskItem("TestFiles/Pages/Profile.razor.css"),
                }
            };

            // Act
            var result = taskInstance.Execute();

            // Assert
            result.Should().BeTrue();
            taskInstance.DiscoveredScopedCssInputs.Should().HaveCount(3);
        }

        [Fact]
        public void DoesNotDiscoversScopedCssFilesForViews_IfFeatureIsUnsupported()
        {
            // Arrange
            var taskInstance = new DiscoverDefaultScopedCssItems()
            {
                Content = new[]
                {
                    new TaskItem("TestFiles/Pages/Counter.cshtml.css"),
                    new TaskItem("TestFiles/Pages/Index.cshtml.css"),
                    new TaskItem("TestFiles/Pages/Profile.cshtml.css"),
                }
            };

            // Act
            var result = taskInstance.Execute();

            // Assert
            result.Should().BeTrue();
            taskInstance.DiscoveredScopedCssInputs.Should().BeEmpty();
        }

        [Fact]
        public void DiscoversScopedCssFilesForViews_BasedOnTheirExtension()
        {
            // Arrange
            var taskInstance = new DiscoverDefaultScopedCssItems()
            {
                SupportsScopedCshtmlCss = true,
                Content = new[]
                {
                    new TaskItem("TestFiles/Pages/Counter.cshtml.css"),
                    new TaskItem("TestFiles/Pages/Index.cshtml.css"),
                    new TaskItem("TestFiles/Pages/Profile.cshtml.css"),
                }
            };

            // Act
            var result = taskInstance.Execute();

            // Assert
            result.Should().BeTrue();
            taskInstance.DiscoveredScopedCssInputs.Should().HaveCount(3);
        }

        [Fact]
        public void DiscoversScopedCssFilesForViews_SkipsFilesWithScopedAttributeWithAFalseValue()
        {
            // Arrange
            var taskInstance = new DiscoverDefaultScopedCssItems()
            {
                SupportsScopedCshtmlCss = true,
                Content = new[]
                {
                    new TaskItem("TestFiles/Pages/Counter.cshtml.css"),
                    new TaskItem("TestFiles/Pages/Index.cshtml.css"),
                    new TaskItem("TestFiles/Pages/Profile.cshtml.css", new Dictionary<string,string>{ ["Scoped"] = "false" }),
                }
            };

            // Act
            var result = taskInstance.Execute();

            // Assert
            result.Should().BeTrue();
            taskInstance.DiscoveredScopedCssInputs.Should().HaveCount(2);
        }
    }
}
