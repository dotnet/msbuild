// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.Build.Framework;
using System;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAResolvePackageAssetsTask
    {
        [Fact]
        public void ItHashesAllParameters()
        {
            var inputProperties = typeof(ResolvePackageAssets)
                .GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public)
                .Where(p => !p.IsDefined(typeof(OutputAttribute)))
                .OrderBy(p => p.Name, StringComparer.Ordinal);

            var requiredProperties = inputProperties
                .Where(p => p.IsDefined(typeof(RequiredAttribute)));

            var task = new ResolvePackageAssets();

            // Initialize all required properties as a genuine task invocation would. We do this
            // because HashSettings need not defend against required parameters being null.
            foreach (var property in requiredProperties)
            {
                property.PropertyType.Should().Be(
                    typeof(string), 
                    because: $"this test hasn't been updated to handle non-string required task parameters like {property.Name}");

                property.SetValue(task, "_");
            }

            byte[] oldHash;
            try
            {
                 oldHash = task.HashSettings();
            }
            catch (ArgumentNullException)
            {
                Assert.True(
                    false, 
                    "HashSettings is likely not correctly handling null value of one or more optional task parameters");

                throw; // unreachable
            }

            foreach (var property in inputProperties)
            {
                switch (property.PropertyType)
                {
                    case var t when t == typeof(bool):
                        property.SetValue(task, true);
                        break;

                    case var t when t == typeof(string):
                        property.SetValue(task, property.Name);
                        break;

                    case var t when t == typeof(ITaskItem[]):
                        property.SetValue(task, new[] { new MockTaskItem() { ItemSpec = property.Name } });
                        break;

                    default:
                        Assert.True(false, $"{property.Name} is not a bool or string or ITaskItem[]. Update the test code to handle that.");
                        throw null; // unreachable
                }

                byte[] newHash = task.HashSettings();
                newHash.Should().NotBeEquivalentTo(
                    oldHash, 
                    because: $"{property.Name} should be included in hash.");

                oldHash = newHash;
            }
        }
    }
}

