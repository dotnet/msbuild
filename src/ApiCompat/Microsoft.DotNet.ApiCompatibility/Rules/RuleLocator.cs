// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Jab;
using Microsoft.DotNet.ApiCompatibility.Abstractions;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    [ServiceProvider(RootServices = new[] { typeof(IEnumerable<IRule>) })]
    [Singleton(typeof(RuleSettings), Instance = nameof(RuleSettings))]
    [Singleton(typeof(RuleRunnerContext), Instance = nameof(RuleRunnerContext))]
    [Singleton(typeof(IRule), typeof(AssemblyIdentityMustMatch))]
    [Singleton(typeof(IRule), typeof(CannotAddAbstractMember))]
    [Singleton(typeof(IRule), typeof(CannotAddMemberToInterface))]
    [Singleton(typeof(IRule), typeof(CannotAddOrRemoveVirtualKeyword))]
    [Singleton(typeof(IRule), typeof(CannotRemoveBaseTypeOrInterface))]
    [Singleton(typeof(IRule), typeof(CannotSealType))]
    [Singleton(typeof(IRule), typeof(EnumsMustMatch))]
    [Singleton(typeof(IRule), typeof(MembersMustExist))]
    internal partial class RuleLocator
    {
        public readonly RuleRunnerContext RuleRunnerContext;
        public readonly RuleSettings RuleSettings;

        public RuleLocator(RuleRunnerContext ruleRunnerContext, RuleSettings ruleSettings)
        {
            RuleRunnerContext = ruleRunnerContext;
            RuleSettings = ruleSettings;
        }
    }
}
