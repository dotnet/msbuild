// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    /// <summary>
    /// Interface representing the context of the <see cref="IRuleRunner"/> used to register and run rule actions.
    /// </summary>
    public interface IRuleContext : IRuleRegistrationContext, IRuleRunnerContext
    {
    }
}
