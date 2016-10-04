// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using FluentAssertions;
using Microsoft.DotNet.Cli.CommandResolution;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace Microsoft.DotNet.Tests
{
    public class GivenAProjectToolsCommandResolverPolicy
    {
        [Fact]
        public void It_contains_resolvers_in_the_right_order()
        {
            var projectToolsCommandResolverPolicy = new ProjectToolsCommandResolverPolicy();
            var defaultCommandResolver = projectToolsCommandResolverPolicy.CreateCommandResolver();

            var resolvers = defaultCommandResolver.OrderedCommandResolvers;

            resolvers.Should().HaveCount(7);

            resolvers.Select(r => r.GetType())
                .Should()
                .ContainInOrder(
                    new []{
                        typeof(MuxerCommandResolver),
                        typeof(RootedCommandResolver),
                        typeof(AppBaseDllCommandResolver),
                        typeof(AppBaseCommandResolver),
                        typeof(PathCommandResolver),
                        typeof(PublishedPathCommandResolver),
                        typeof(ProjectToolsCommandResolver)
                    });
        }
    }
}
