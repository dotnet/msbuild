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
                BuildEngine = engine,
                Items = compile,
                ItemName = "Compile",
                PropertyNameToDisableDefaultItems = "PropertyNameToDisableDefaultItems",
                MoreInformationLink = "MoreInformationLink",
                DefaultItemsEnabled = true,
                DefaultItemsOfThisTypeEnabled = true
            };
            task.Execute().Should().BeFalse();

            engine.Errors.Count.Should().Be(1);
            engine.Errors[0].Code.Should().Be("NETSDK1022");
            engine.Errors[0].Message.Should().EndWith("The duplicate items were: 'foo.cs'");

            task.DeduplicatedItems.Length.Should().Be(1);
        }
    }
}
