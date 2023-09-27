// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ApiCompatibility.Logging;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    /// <summary>
    /// The default rule factory that returns all available rules with the given input settings.
    /// </summary>
    public class RuleFactory : IRuleFactory
    {
        private readonly ISuppressibleLog _log;
        private readonly bool _enableRuleAttributesMustMatch;
        private readonly bool _enableRuleCannotChangeParameterName;

        public RuleFactory(ISuppressibleLog log,
            bool enableRuleAttributesMustMatch = false,
            bool enableRuleCannotChangeParameterName = false)
        {
            _log = log;
            _enableRuleAttributesMustMatch = enableRuleAttributesMustMatch;
            _enableRuleCannotChangeParameterName = enableRuleCannotChangeParameterName;
        }

        /// <inheritdoc />
        public IRule[] CreateRules(IRuleSettings settings, IRuleRegistrationContext context)
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
                new CannotChangeVisibility(settings, context)
            };

            if (_enableRuleAttributesMustMatch)
            {
                rules.Add(new AttributesMustMatch(settings, context));
            }

            if (_enableRuleCannotChangeParameterName)
            {
                rules.Add(new CannotChangeParameterName(settings, context));
            }

            return rules.ToArray();
        }
    }
}
