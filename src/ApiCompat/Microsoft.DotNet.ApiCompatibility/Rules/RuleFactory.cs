// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.ApiCompatibility.Logging;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    /// <summary>
    /// The default rule factory that returns all available rules with the given input settings.
    /// </summary>
    public class RuleFactory : IRuleFactory
    {
        private readonly ICompatibilityLogger _log;

        public RuleFactory(ICompatibilityLogger log)
        {
            _log = log;
        }

        /// <inheritdoc />
        public IRule[] CreateRules(RuleSettings settings, IRuleRegistrationContext context)
        {
            return new IRule[]
            {
                new AssemblyIdentityMustMatch(_log, settings, context),
                new CannotAddAbstractMember(settings, context),
                new CannotAddMemberToInterface(settings, context),
                new CannotAddOrRemoveVirtualKeyword(settings, context),
                new CannotRemoveBaseTypeOrInterface(settings, context),
                new CannotSealType(settings, context),
                new EnumsMustMatch(settings, context),
                new MembersMustExist(settings, context)
            };
        }
    }
}
