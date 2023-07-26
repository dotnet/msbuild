// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.CommandFactory;

namespace Microsoft.DotNet.Tests
{
    public class GivenAScriptCommandResolver
    {
        [Fact]
        public void It_contains_resolvers_in_the_right_order()
        {
            var scriptCommandResolver = ScriptCommandResolverPolicy.Create();

            var resolvers = scriptCommandResolver.OrderedCommandResolvers;

            resolvers.Should().HaveCount(5);

            resolvers.Select(r => r.GetType())
                .Should()
                .ContainInOrder(
                    new[]{
                        typeof(RootedCommandResolver),
                        typeof(MuxerCommandResolver),
                        typeof(ProjectPathCommandResolver),
                        typeof(AppBaseCommandResolver),
                        typeof(PathCommandResolver)
                    });
        }
    }
}
