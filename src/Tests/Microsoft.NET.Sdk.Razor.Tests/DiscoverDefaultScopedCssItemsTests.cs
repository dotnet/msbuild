// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using FluentAssertions;
using Microsoft.Build.Utilities;
using Microsoft.AspNetCore.Razor.Tasks;
using Xunit;

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
        public void DiscoversScopedCssFiles_SkipsFilesWithScopedAttributeWithAFalseValue()
        {
            // Arrange
            var taskInstance = new DiscoverDefaultScopedCssItems()
            {
                Content = new[]
                {
                    new TaskItem("TestFiles/Pages/Counter.razor.css"),
                    new TaskItem("TestFiles/Pages/Index.razor.css"),
                    new TaskItem("TestFiles/Pages/Profile.razor.css", new Dictionary<string,string>{ ["Scoped"] = "false" }),
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
