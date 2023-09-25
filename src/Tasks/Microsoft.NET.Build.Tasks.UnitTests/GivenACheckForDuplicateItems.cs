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
                MoreInformationLink = "MoreInformationLink"
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

            // execute task
            var task = new CheckForDuplicateItems()
            {
                Items = compile,
                ItemName = "Compile",
                PropertyNameToDisableDefaultItems = "PropertyNameToDisableDefaultItems",
                MoreInformationLink = "MoreInformationLink"
            };
            task.Execute().Should().BeFalse();

            task.DeduplicatedItems.Length.Should().Be(1);
        }

    }
}
