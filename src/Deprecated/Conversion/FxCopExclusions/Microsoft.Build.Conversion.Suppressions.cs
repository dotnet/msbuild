// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// FxCop Suppression file
// To Use:
// Add module level suppressions to this file to have them suppressed in the assembly
//
using System.Diagnostics.CodeAnalysis;

#if CODE_ANALYSIS
[module: SuppressMessage("Microsoft.Naming","CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId="0", Scope="module", Target="microsoft.build.conversion.v12.0.dll", Justification="Already shipped for several versions with a name like this")]
[module: SuppressMessage("Microsoft.Naming","CA1709:IdentifiersShouldBeCasedCorrectly", MessageId="v", Justification="Spelled correctly")]
[module: SuppressMessage("Microsoft.Naming","CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId="5", Justification="Spelled correctly")]
[module: SuppressMessage("Microsoft.Design", "CA2210:AssembliesShouldHaveValidStrongNames", Justification="We delay sign our assemblies.")]
[module: SuppressMessage("Microsoft.MSInternal", "CA905:SystemAndMicrosoftNamespacesRequireApproval", Scope="namespace", Target="Microsoft.Build.Conversion", Justification="Microsoft.Build.Conversion is an approved namespace according to http://ddwww/apps/apiowners/")]
#endif

