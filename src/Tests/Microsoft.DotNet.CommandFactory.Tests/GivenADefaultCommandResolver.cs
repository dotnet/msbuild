// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.CommandFactory;

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
                    new[]{
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
