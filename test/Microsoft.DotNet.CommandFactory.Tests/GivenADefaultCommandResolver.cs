// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using FluentAssertions;
using Microsoft.DotNet.CommandFactory;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace Microsoft.DotNet.Tests
{
    public class GivenADefaultCommandResolver
    {
        [Fact]
        public void It_contains_resolvers_in_the_right_order()
        {
            var defaultCommandResolver = DefaultCommandResolverPolicy.Create();

            var resolvers = defaultCommandResolver.OrderedCommandResolvers;

            resolvers.Should().HaveCount(9);

            resolvers.Select(r => r.GetType())
                .Should()
                .ContainInOrder(
                    new []{
                        typeof(MuxerCommandResolver),
                        typeof(DotnetToolsCommandResolver),
                        typeof(LocalToolsCommandResolver),
                        typeof(RootedCommandResolver),
                        typeof(ProjectToolsCommandResolver),
                        typeof(AppBaseDllCommandResolver),
                        typeof(AppBaseCommandResolver),
                        typeof(PathCommandResolver),
                        typeof(PublishedPathCommandResolver)
                    });
        }
    }
}
