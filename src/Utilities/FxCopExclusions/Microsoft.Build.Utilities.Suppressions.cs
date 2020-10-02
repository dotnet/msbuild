// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// FxCop Suppression file
// To Use:
// Add module level suppressions to this file to have them suppressed in the assembly
//

#if CODE_ANALYSIS
[module: SuppressMessage("Microsoft.Naming","CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId="0", Scope="module", Target="microsoft.build.utilities.core.dll", Justification="It's been named this way for several versions now.")]
[module: SuppressMessage("Microsoft.Naming","CA1709:IdentifiersShouldBeCasedCorrectly", MessageId="v", Justification="v in v3.5 is correctly cased")]
[module: SuppressMessage("Microsoft.Naming","CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId="5", Justification="5 in v3.5 is correctly spelled")]
[module: SuppressMessage("Microsoft.Design", "CA2210:AssembliesShouldHaveValidStrongNames", Justification="We delay sign our assemblies.")]
[module: SuppressMessage("Microsoft.Naming","CA1709:IdentifiersShouldBeCasedCorrectly", MessageId="SDK", Scope="member", Target="Microsoft.Build.Utilities.ToolLocationHelper.#GetInstalledSDKLocations()", Justification="SDK is the proper casing")]
[module: SuppressMessage("Microsoft.Naming","CA1709:IdentifiersShouldBeCasedCorrectly", MessageId="SDK", Scope="member", Target="Microsoft.Build.Utilities.ToolLocationHelper.#GetInstalledSDKLocations(System.String,System.String,System.String,System.String)", Justification="SDK is the proper casing")]
[module: SuppressMessage("Microsoft.Naming","CA1709:IdentifiersShouldBeCasedCorrectly", MessageId="SDK", Scope="member", Target="Microsoft.Build.Utilities.ToolLocationHelper.#GetInstalledSDKLocation(System.String)", Justification="SDK casing is correct")]
[module: SuppressMessage("Microsoft.Naming","CA1709:IdentifiersShouldBeCasedCorrectly", MessageId="SDK", Scope="member", Target="Microsoft.Build.Utilities.ToolLocationHelper.#GetInstalledSDKLocation(System.String,System.String,System.String)", Justification="SDK casing is correct")]
[module: SuppressMessage("Microsoft.Naming","CA1709:IdentifiersShouldBeCasedCorrectly", MessageId="SDK", Scope="member", Target="Microsoft.Build.Utilities.ToolLocationHelper.#GetInstalledSDKLocation(System.String,System.String,System.String,System.String,System.String)", Justification="SDK casing is correct")]
[module: SuppressMessage("Microsoft.Naming","CA1709:IdentifiersShouldBeCasedCorrectly", MessageId="SDK", Scope="member", Target="Microsoft.Build.Utilities.ToolLocationHelper.#GetInstalledSDKLocations(System.String,System.String)", Justification="SDK casing is correct")]
[module: SuppressMessage("Microsoft.Naming","CA1709:IdentifiersShouldBeCasedCorrectly", MessageId="SDK", Scope="member", Target="Microsoft.Build.Utilities.ToolLocationHelper.#GetWindowsSDKMetadataFolderLocations()", Justification="SDK casing is correct")]

#endif

