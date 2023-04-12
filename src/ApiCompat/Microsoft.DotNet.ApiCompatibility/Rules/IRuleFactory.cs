// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    /// <summary>
    /// Factory to create api comparison rules.
    /// </summary>
    public interface IRuleFactory
    {
        /// <summary>
        /// Creates api copmarison rules based on the given settings and registers actions via the provided context.
        /// </summary>
        /// <param name="settings">The rule settings.</param>
        /// <param name="context">The rule registration context that allows rules to register actions.</param>
        /// <returns></returns>
        IRule[] CreateRules(RuleSettings settings, IRuleRegistrationContext context);
    }
}
