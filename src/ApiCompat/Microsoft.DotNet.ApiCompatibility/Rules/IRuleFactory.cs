// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    /// <summary>
    /// Factory to create api comparison rules.
    /// </summary>
    public interface IRuleFactory
    {
        /// <summary>
        /// Creates api comparison rules based on the given settings and registers actions via the provided context.
        /// </summary>
        /// <param name="settings">The rule settings.</param>
        /// <param name="context">The rule registration context that allows rules to register actions.</param>
        /// <returns></returns>
        IRule[] CreateRules(IRuleSettings settings, IRuleRegistrationContext context);
    }
}
