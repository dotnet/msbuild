// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    /// <summary>
    /// Interface representing the context of the <see cref="IRuleRunner"/> used to register and run rule actions.
    /// </summary>
    public interface IRuleContext : IRuleRegistrationContext, IRuleRunnerContext
    {
    }
}
