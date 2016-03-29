using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli.Compiler.Common;
using Xunit;
using FluentAssertions;

using AssemblyReferenceInfo = Microsoft.DotNet.Cli.Compiler.Common.BindingRedirectGenerator.AssemblyReferenceInfo;
using AssemblyIdentity = Microsoft.DotNet.Cli.Compiler.Common.BindingRedirectGenerator.AssemblyIdentity;

namespace Microsoft.DotNet.Tools.Compiler.Tests
{
    public class BindingRedirectGeneratorTests
    {
        [Fact]
        public void ResolvesDuplicatesUsingFirstOccurence()
        {
            var hash = "01234qwerty";
            var redirects = BindingRedirectGenerator.CollectRedirects(new[]
            {
                new AssemblyReferenceInfo(
                    new AssemblyIdentity("A", new Version(1, 5), "en-US", hash),
                    new []
                    {
                        new AssemblyIdentity("B", new Version(1, 1), "en-US", hash),
                    }
                ),
                new AssemblyReferenceInfo(
                    new AssemblyIdentity("B", new Version(1, 4), "en-US", hash), new AssemblyIdentity[] {}
                ),
                new AssemblyReferenceInfo(
                    new AssemblyIdentity("B", new Version(1, 5), "en-US", hash), new AssemblyIdentity[] {}
                ),
                new AssemblyReferenceInfo(
                    new AssemblyIdentity("C", new Version(1, 5), "en-US", hash),
                    new []
                    {
                        new AssemblyIdentity("B", new Version(1, 3), "en-US", hash),
                    }
                )
            });

            redirects.Should().HaveCount(2);
            redirects.Should().Contain(r => 
                r.From.Version == new Version(1, 1) && 
                r.From.Name == "B" &&
                r.To.Version == new Version(1, 4) &&
                r.To.Name == "B"
                );
            redirects.Should().Contain(r =>
                r.From.Version == new Version(1, 3) &&
                r.From.Name == "B" &&
                r.To.Version == new Version(1, 4) &&
                r.To.Name == "B"
                );
        }
    }
}
