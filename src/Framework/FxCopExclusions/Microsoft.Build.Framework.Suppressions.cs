// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// FxCop Suppression file
// To Use:
// Add add module level suppressions to this file to have them suppressed in the assembly

#if CODE_ANALYSIS
[module: SuppressMessage("Microsoft.Design", "CA2210:AssembliesShouldHaveValidStrongNames")]
[module: SuppressMessage("Microsoft.MSInternal", "CA905:SystemAndMicrosoftNamespacesRequireApproval", Scope="namespace", Target="Microsoft.Build.CommandLine", Justification="This is an approved namespace.")]
[module: SuppressMessage("Microsoft.Naming","CA1709:IdentifiersShouldBeCasedCorrectly", MessageId="STA", Scope="type", Target="Microsoft.Build.Framework.RunInSTAAttribute", Justification="Not worth breaking custormers because of case.")]
#endif
