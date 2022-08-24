// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.ApiCompatibility.Logging;
using System.Collections.Generic;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    /// <summary>
    /// The default rule factory that returns all available rules with the given input settings.
    /// </summary>
    public class RuleFactory : IRuleFactory
    {
        private readonly ICompatibilityLogger _log;
        private readonly IReadOnlyCollection<string>? _excludeAttributesFiles;
        private readonly bool _enableRuleCannotChangeParameterName;

        public RuleFactory(ICompatibilityLogger log, IReadOnlyCollection<string>? excludeAttributesFiles = null, bool enableRuleCannotChangeParameterName = false)
        {
            _log = log;
            _excludeAttributesFiles = excludeAttributesFiles;
            _enableRuleCannotChangeParameterName = enableRuleCannotChangeParameterName;
        }

        /// <inheritdoc />
        public IRule[] CreateRules(RuleSettings settings, IRuleRegistrationContext context)
        {
            List<IRule> rules = new()
            {
                new AssemblyIdentityMustMatch(_log, settings, context),
                new CannotAddAbstractMember(settings, context),
                new CannotAddMemberToInterface(settings, context),
                new CannotAddOrRemoveVirtualKeyword(settings, context),
                new CannotRemoveBaseTypeOrInterface(settings, context),
                new CannotSealType(settings, context),
                new EnumsMustMatch(settings, context),
                new MembersMustExist(settings, context),
                new AttributesMustMatch(settings, context, _excludeAttributesFiles),
            };

            if (_enableRuleCannotChangeParameterName)
            {
                rules.Add(new CannotChangeParameterName(settings, context));
            }

            return rules.ToArray();
        }
    }
}
