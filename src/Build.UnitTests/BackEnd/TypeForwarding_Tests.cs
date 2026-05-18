// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Build.Execution;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Engine.UnitTests.BackEnd
{
    public class TypeForwarding_Tests
    {
        private static readonly HashSet<string> s_forwardedTypeNames = typeof(BuildManager).Assembly
            .GetForwardedTypes()
            .Select(type => type.FullName!)
            .ToHashSet(StringComparer.Ordinal);

        [Theory]
        [InlineData("Microsoft.Build.Internal.Handshake")]
        [InlineData("Microsoft.Build.Internal.HandshakeComponents")]
        [InlineData("Microsoft.Build.Internal.HandshakeOptions")]
        [InlineData("Microsoft.Build.Internal.HandshakeStatus")]
        [InlineData("Microsoft.Build.Internal.ServerNodeHandshake")]
        [InlineData("Microsoft.Build.BackEnd.INodePacket")]
        [InlineData("Microsoft.Build.BackEnd.NodePacketType")]
        [InlineData("Microsoft.Build.BackEnd.NodePacketTypeExtensions")]
        [InlineData("Microsoft.Build.Internal.CommunicationsUtilities")]
        [InlineData("Microsoft.Build.Shared.XMakeAttributes")]
        [InlineData("Microsoft.Build.Shared.XMakeElements")]
        public void BuildAssemblyContainsExpectedTypeForwarders(string typeName)
        {
            s_forwardedTypeNames.ShouldContain(typeName);
        }
    }
}
