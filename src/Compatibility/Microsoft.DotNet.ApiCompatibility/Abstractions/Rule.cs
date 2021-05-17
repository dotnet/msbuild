// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.ApiCompatibility.Rules;

namespace Microsoft.DotNet.ApiCompatibility.Abstractions
{
    /// <summary>
    /// Base class for Rules to use in order to be discovered and invoked by the <see cref="IRuleRunner"/>
    /// </summary>
    public abstract class Rule
    {
        /// <summary>
        //// Method that is called when the rules are created by the <see cref="IRuleRunner"/> in
        /// order to do the initial setup for the rule. This method stores the rule settings and calls
        /// <see cref="Initialize(RuleRunnerContext)"/>.
        /// </summary>
        /// <param name="context">The context containing callbacks and settings for the rule to use.</param>
        /// <param name="settings">The settings for the rule.</param>
        public void Setup(RuleRunnerContext context, RuleSettings settings)
        {
            Settings = settings;
            Initialize(context);
        }

        /// <summary>
        /// Method that is called when the rules are created by the <see cref="IRuleRunner"/> in
        /// order to do the initial setup for the rule.
        /// </summary>
        /// <param name="context">The context that the <see cref="IRuleRunner"/> creates holding callbacks to get the differences.</param>
        public abstract void Initialize(RuleRunnerContext context);

        /// <summary>
        /// Settings use by the rules to determine how they should calculate differences.
        /// </summary>
        public RuleSettings Settings { get; private set; }
    }
}
