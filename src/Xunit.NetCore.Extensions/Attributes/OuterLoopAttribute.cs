// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Xunit.Sdk;

namespace Xunit
{
    /// <summary>
    /// Apply this attribute to your test method to specify a outer-loop category.
    /// </summary>
    [TraitDiscoverer("Xunit.NetCore.Extensions.OuterLoopTestsDiscoverer", "Xunit.NetCore.Extensions")]
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
    public class OuterLoopAttribute : Attribute, ITraitAttribute
    {
        public OuterLoopAttribute() { }
        public OuterLoopAttribute(string reason) { }
    }
}
