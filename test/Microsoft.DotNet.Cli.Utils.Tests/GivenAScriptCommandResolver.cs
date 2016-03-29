// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Xunit;
using Moq;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.Extensions.PlatformAbstractions;
using System.Threading;
using FluentAssertions;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Cli.Utils.Tests
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
                    new []{
                        typeof(RootedCommandResolver),
                        typeof(MuxerCommandResolver),
                        typeof(ProjectPathCommandResolver),
                        typeof(AppBaseCommandResolver),
                        typeof(PathCommandResolver)
                    });
        }
    }
}
