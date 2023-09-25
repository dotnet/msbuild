// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenACheckForDuplicateItems
    {
        [Fact]
        public void CheckForNoDuplicateItems()
        {
            var compile = new[]
            {
                new TaskItem("foo.cs"),
                new TaskItem("bar.cs"),
            };

            // execute task
            var task = new CheckForDuplicateItems()
            {
                Items = compile,
                ItemName = "Compile",
                PropertyNameToDisableDefaultItems = "PropertyNameToDisableDefaultItems",
                MoreInformationLink = "MoreInformationLink",
                DefaultItemsEnabled = true,
                DefaultItemsOfThisTypeEnabled = true
            };
            task.Execute().Should().BeTrue();

            task.DeduplicatedItems.Length.Should().Be(0);
        }

        [Fact]
        public void CheckForDuplicateItems()
        {
            var compile = new[]
            {
                new TaskItem("foo.cs"),
                new TaskItem("FOO.cs"),
            };
            var engine = new MockBuildEngine();

            // execute task
            var task = new CheckForDuplicateItems()
            {
                engine = engine,
                Items = compile,
                ItemName = "Compile",
                PropertyNameToDisableDefaultItems = "PropertyNameToDisableDefaultItems",
                MoreInformationLink = "MoreInformationLink",
                DefaultItemsEnabled = true,
                DefaultItemsOfThisTypeEnabled = true
            };
            task.Execute().Should().BeFalse();

            engine.Errors.Count.Should().Be(1);
            engine.Errors[0].Message.Should().Be("Duplicate 'Compile' items were included. The .NET SDK includes 'Compile' items from your project directory by default. You can either remove these items from your project file, or set the 'PropertyNameToDisableDefaultItems' property to 'false' if you want to explicitly include them in your project file. For more information, see MoreInformationLink. The duplicate items were: 'foo.cs'");

            task.DeduplicatedItems.Length.Should().Be(1);
        }
    }
}
