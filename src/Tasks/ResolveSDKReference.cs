// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Resolves an SDKReference to a full path on disk</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Resolves an SDKReference to a full path on disk
    /// </summary>
    public class ResolveSDKReference : TaskExtension
    {
        #region fields

        ///<summary>
        /// Regex for breaking up the sdk reference include into pieces.
        /// Example: XNA, Version=8.0
        /// </summary>
        private static readonly Regex s_sdkReferenceFormat = new Regex
        (
             @"(?<SDKSIMPLENAME>^[^,]*),\s*Version=(?<SDKVERSION>.*)",
            RegexOptions.IgnoreCase
        );

        /// <summary>
        /// SimpleName group
        /// </summary>
        private const string SDKsimpleNameGroup = "SDKSIMPLENAME";

        /// <summary>
        /// Version group
        /// </summary>
        private const string SDKVersionGroup = "SDKVERSION";

        /// <summary>
        /// Delimiter used to delimit the dependent sdk's in the warning message
        /// </summary>
        private const string CommaSpaceDelimiter = ", ";

        /// <summary>
        /// Split char for the appx attribute
        /// </summary>
        private static readonly char[] s_appxSplitChar = { '-' };

        /// <summary>
        /// SDKName
        /// </summary>
        private const string SDKName = "SDKName";

        /// <summary>
        /// PlatformVersion
        /// </summary>
        private const string SDKPlatformVersion = "PlatformVersion";

        /// <summary>
        /// Split char for strings
        /// </summary>
        private static readonly char[] s_semicolonSplitChar = { ';' };

        /// <summary>
        /// Default target platform version
        /// </summary>
        private static readonly Version s_defaultTargetPlatformVersion = new Version("7.0");

        /// <summary>
        ///  Set of sdk references to resolve to paths on disk.
        /// </summary>
        private ITaskItem[] _sdkReferences = Array.Empty<ITaskItem>();

        /// <summary>
        /// The list of installed SDKs the location of the SDK, the SDKName metadata is the SDKName.
        /// </summary>
        private ITaskItem[] _installedSDKs = Array.Empty<ITaskItem>();

        /// <summary>
        /// stores value of TargetPlatformVersion property
        /// </summary>
        private Version _targetPlatformVersion;

        /// <summary>
        /// Stores TargetPlatform property
        /// </summary>
        private string _targetPlatformIdentifier;

        /// <summary>
        /// Stores ProjectName property
        /// </summary>
        private string _projectName;

        /// <summary>
        /// Stores dictionary with runtime only reference dependencies
        /// </summary>
        private Dictionary<string, string> _runtimeReferenceOnlyDependenciesByName;

        #endregion

        #region Properties

        /// <summary>
        /// Set of SDK References to resolve to paths on disk
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "SDK", Justification = "Shipped this way in Dev11 Beta (go-live)")]
        [Required]
        public ITaskItem[] SDKReferences
        {
            get => _sdkReferences;

            set
            {
                ErrorUtilities.VerifyThrowArgumentNull(value, nameof(SDKReferences));
                _sdkReferences = value;
            }
        }

        /// <summary>
        /// The list of installed SDKs the location of the SDK, the SDKName metadata is the SDKName.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "SDK", Justification = "Shipped this way in Dev11 Beta (go-live)")]
        [Required]
        public ITaskItem[] InstalledSDKs
        {
            get => _installedSDKs;

            set
            {
                ErrorUtilities.VerifyThrowArgumentNull(value, nameof(InstalledSDKs));
                _installedSDKs = value;
            }
        }

        /// <summary>
        /// TargetPlatform used in warning/error messages
        /// </summary>
        [Required]
        public string TargetPlatformIdentifier
        {
            get
            {
                _targetPlatformIdentifier = _targetPlatformIdentifier ?? String.Empty;
                return _targetPlatformIdentifier;
            }

            set => _targetPlatformIdentifier = value;
        }

        /// <summary>
        /// ProjectName used in warning/error messages
        /// </summary>
        [Required]
        public string ProjectName
        {
            get
            {
                _projectName = _projectName ?? String.Empty;
                return _projectName;
            }

            set => _projectName = value;
        }

        /// <summary>
        /// TargetPlatformVersion property used to filter SDKs
        /// </summary>
        [Required]
        public string TargetPlatformVersion
        {
            get => TargetPlatformAsVersion.ToString();

            set
            {
                if (Version.TryParse(value, out Version versionValue))
                {
                    TargetPlatformAsVersion = versionValue;
                }
            }
        }

        /// <summary>
        /// Reference may be passed in so their SDKNames can be resolved and then sdkroot paths can be tacked onto the reference
        /// so RAR can find the assembly correctly in the sdk location.
        /// </summary>
        public ITaskItem[] References { get; set; }

        /// <summary>
        /// List of disallowed dependencies passed from the targets file (deprecated)
        /// For instance "VCLibs 11" should be disallowed in projects targeting Win 8.1 or higher.
        /// </summary>
        public ITaskItem[] DisallowedSDKDependencies { get; set; }

        /// <summary>
        /// List of dependencies passed from the targets file that will have the metadata RuntimeReferenceOnly set as true. 
        /// For instance "VCLibs 11" should have such a metadata set to true in projects targeting Win 8.1 or higher.
        /// </summary>
        public ITaskItem[] RuntimeReferenceOnlySDKDependencies { get; set; }

        /// <summary>
        /// Configuration for SDK's which are resolved
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "SDK", Justification = "Shipped this way in Dev11 Beta (go-live)")]
        public string TargetedSDKConfiguration { get; set; }

        /// <summary>
        /// Architecture of the SDK's we are targeting
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "SDK", Justification = "Shipped this way in Dev11 Beta (go-live)")]
        public string TargetedSDKArchitecture { get; set; }

        /// <summary>
        /// Enables warning when MaxPlatformVersion is not present in the manifest and the ESDK platform version (from its path) 
        /// is different than the target platform version (from the project)
        /// </summary>
        public bool WarnOnMissingPlatformVersion { get; set; }

        /// <summary>
        /// Should problems resolving SDKs be logged as a warning or an error.
        /// If the resolution problem is logged as an error the build will fail.
        /// If the resolution problem is logged as a warning we will warn and continue.
        /// </summary>
        public bool LogResolutionErrorsAsWarnings { get; set; }

        /// <summary>
        /// The prefer32bit flag used during the build
        /// </summary>
        public bool Prefer32Bit { get; set; }

        /// <summary>
        /// Resolved SDK References
        /// </summary>
        [Output]
        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "SDK", Justification = "Shipped this way in Dev11 Beta (go-live)")]
        public ITaskItem[] ResolvedSDKReferences { get; private set; }

        /// <summary>
        /// Version object containing target platform version
        /// </summary>
        private Version TargetPlatformAsVersion
        {
            get
            {
                _targetPlatformVersion = _targetPlatformVersion ?? s_defaultTargetPlatformVersion;
                return _targetPlatformVersion;
            }

            set => _targetPlatformVersion = value;
        }

        #endregion

        /// <summary>
        /// Execute the task.
        /// </summary>
        public override bool Execute()
        {
            ResolvedSDKReferences = Array.Empty<ITaskItem>();

            if (InstalledSDKs.Length == 0)
            {
                Log.LogMessageFromResources("ResolveSDKReference.NoSDKLocationsSpecified");
                return true;
            }

            _runtimeReferenceOnlyDependenciesByName = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

            if (RuntimeReferenceOnlySDKDependencies != null)
            {
                foreach (ITaskItem runtimeDependencyOnlyItem in RuntimeReferenceOnlySDKDependencies)
                {
                    if (ParseSDKReference(runtimeDependencyOnlyItem.ItemSpec, out string dependencyName, out string dependencyVersion))
                    {
                        _runtimeReferenceOnlyDependenciesByName[dependencyName] = dependencyVersion;
                    }
                }
            }

            // Convert the list of installed SDK's to a dictionary for faster lookup
            var sdkItems = new Dictionary<string, ITaskItem>(InstalledSDKs.Length, StringComparer.OrdinalIgnoreCase);

            foreach (ITaskItem installedsdk in InstalledSDKs)
            {
                string installLocation = installedsdk.ItemSpec;
                string sdkName = installedsdk.GetMetadata(SDKName);

                if (installLocation.Length > 0 && sdkName.Length > 0)
                {
                    sdkItems[sdkName] = installedsdk;
                }
            }

            // We need to check to see if there are any SDKNames on any of the reference items in the project. If there are 
            // then we do not want those SDKs to expand their reference assemblies by default because we are going to use RAR to look inside of them for certain reference assemblies only.
            var sdkNamesOnReferenceItems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (References != null)
            {
                foreach (ITaskItem referenceItem in References)
                {
                    string sdkName = referenceItem.GetMetadata(SDKName);
                    if (sdkName.Length > 0)
                    {
                        sdkNamesOnReferenceItems.Add(sdkName);
                    }
                }
            }

            // The set of reference items declared in the project file, without duplicate entries.
            var sdkReferenceItems = new HashSet<SDKReference>();

            // Maps a product family name to a set of SDKs with that product family name
            var productFamilyNameToSDK = new Dictionary<string, HashSet<SDKReference>>(StringComparer.OrdinalIgnoreCase);

            // Maps a sdk name (no version) to a set of SDKReferences with the same name
            var sdkNameToSDK = new Dictionary<string, HashSet<SDKReference>>(StringComparer.OrdinalIgnoreCase);

            // Set of sdks which are not compatible with other sdks of the same product famuily or with the same sdk name
            var sdksNotCompatibleWithOtherSDKs = new HashSet<SDKReference>();

            // Go through each reference passed in and determine if it is in the set of installed SDKs. 
            // Also create new output items if the item is in an installed SDK and set the metadata correctly.
            foreach (ITaskItem referenceItem in SDKReferences)
            {
                // Parse the SDK reference item include. The name could have been added by a user and may have extra spaces or be not well formatted.
                SDKReference reference = ParseSDKReference(referenceItem);

                // Could not parse the reference, lets skip over this reference item. An error would have been logged in the ParseSDKReference method to tell the 
                // user why the parsing did not happen.
                if (reference == null)
                {
                    continue;
                }

                // Make sure we do not include a duplicate reference item if one has already been seen in the project file.
                if (!sdkReferenceItems.Contains(reference) /* filter out duplicate sdk reference entries*/)
                {
                    sdkReferenceItems.Add(reference);
                    reference.Resolve(sdkItems, TargetedSDKConfiguration, TargetedSDKArchitecture, sdkNamesOnReferenceItems, LogResolutionErrorsAsWarnings, Prefer32Bit, TargetPlatformIdentifier, TargetPlatformAsVersion, ProjectName, WarnOnMissingPlatformVersion);
                    if (reference.Resolved)
                    {
                        if (!String.IsNullOrEmpty(reference.ProductFamilyName))
                        {
                            if (!productFamilyNameToSDK.TryGetValue(reference.ProductFamilyName, out HashSet<SDKReference> sdksWithProductFamilyName))
                            {
                                productFamilyNameToSDK.Add(reference.ProductFamilyName, new HashSet<SDKReference> { reference });
                            }
                            else
                            {
                                sdksWithProductFamilyName.Add(reference);
                            }
                        }

                        if (reference.SupportsMultipleVersions != MultipleVersionSupport.Allow && !reference.SimpleName.Equals("Microsoft.VCLibs", StringComparison.InvariantCultureIgnoreCase))
                        {
                            sdksNotCompatibleWithOtherSDKs.Add(reference);
                        }

                        if (!sdkNameToSDK.TryGetValue(reference.SimpleName, out HashSet<SDKReference> sdksWithSimpleName))
                        {
                            sdkNameToSDK.Add(reference.SimpleName, new HashSet<SDKReference> { reference });
                        }
                        else
                        {
                            sdksWithSimpleName.Add(reference);
                        }
                    }
                }
            }

            // Go through each of the items which have been processed and log the results.
            foreach (SDKReference reference in sdkReferenceItems)
            {
                LogResolution(reference);
            }

            // Go through each of the incompatible references and log the ones that are not compatible with it
            // starting with being incompatible with the product family then with the sdk name.
            foreach (SDKReference notCompatibleReference in sdksNotCompatibleWithOtherSDKs)
            {
                // If we have already error or warned about an sdk not being compatible with one of the notCompatibleReferences then do not log it again
                // an sdk could be incompatible because the productfamily is the same but also be incompatible at the same time due to the sdk name
                // we only want to log one of those cases so we do not get 2 warings or errors for the same sdks.
                var sdksAlreadyErrorOrWarnedFor = new HashSet<SDKReference>();

                // Check to see if a productfamily was set, we want to emit this warning or error first.
                if (!String.IsNullOrEmpty(notCompatibleReference.ProductFamilyName))
                {
                    if (productFamilyNameToSDK.TryGetValue(notCompatibleReference.ProductFamilyName, out HashSet<SDKReference> referenceInProductFamily))
                    {
                        // We want to build a list of incompatible reference names so we can emit them in the error or warnings.
                        var listOfIncompatibleReferences = new List<string>();
                        foreach (SDKReference incompatibleReference in referenceInProductFamily)
                        {
                            if (!sdksAlreadyErrorOrWarnedFor.Contains(incompatibleReference) && incompatibleReference != notCompatibleReference /*cannot be incompatible with self*/)
                            {
                                listOfIncompatibleReferences.Add(String.Format(CultureInfo.CurrentCulture, "\"{0}\"", incompatibleReference.SDKName));
                                sdksAlreadyErrorOrWarnedFor.Add(incompatibleReference);
                            }
                        }

                        // Only log a warning or error if there were incompatible references
                        if (listOfIncompatibleReferences.Count > 0)
                        {
                            string incompatibleReferencesDelimited = String.Join(", ", listOfIncompatibleReferences.ToArray());
                            if (notCompatibleReference.SupportsMultipleVersions == MultipleVersionSupport.Error)
                            {
                                Log.LogErrorWithCodeFromResources("ResolveSDKReference.CannotReferenceTwoSDKsSameFamily", notCompatibleReference.SDKName, incompatibleReferencesDelimited, notCompatibleReference.ProductFamilyName);
                            }
                            else
                            {
                                Log.LogWarningWithCodeFromResources("ResolveSDKReference.CannotReferenceTwoSDKsSameFamily", notCompatibleReference.SDKName, incompatibleReferencesDelimited, notCompatibleReference.ProductFamilyName);
                            }
                        }
                    }
                }

                if (sdkNameToSDK.TryGetValue(notCompatibleReference.SimpleName, out HashSet<SDKReference> referenceWithSameName))
                {
                    // We want to build a list of incompatible reference names so we can emit them in the error or warnings.
                    var listOfIncompatibleReferences = new List<string>();
                    foreach (SDKReference incompatibleReference in referenceWithSameName)
                    {
                        if (!sdksAlreadyErrorOrWarnedFor.Contains(incompatibleReference) && incompatibleReference != notCompatibleReference /*cannot be incompatible with self*/)
                        {
                            listOfIncompatibleReferences.Add(String.Format(CultureInfo.CurrentCulture, "\"{0}\"", incompatibleReference.SDKName));
                            sdksAlreadyErrorOrWarnedFor.Add(incompatibleReference);
                        }
                    }

                    // Only log a warning or error if there were incompatible references
                    if (listOfIncompatibleReferences.Count > 0)
                    {
                        string incompatibleReferencesDelimited = String.Join(", ", listOfIncompatibleReferences.ToArray());
                        if (notCompatibleReference.SupportsMultipleVersions == MultipleVersionSupport.Error)
                        {
                            Log.LogErrorWithCodeFromResources("ResolveSDKReference.CannotReferenceTwoSDKsSameName", notCompatibleReference.SDKName, incompatibleReferencesDelimited);
                        }
                        else
                        {
                            Log.LogWarningWithCodeFromResources("ResolveSDKReference.CannotReferenceTwoSDKsSameName", notCompatibleReference.SDKName, incompatibleReferencesDelimited);
                        }
                    }
                }
            }

            AddMetadataToReferences(Log, sdkReferenceItems, _runtimeReferenceOnlyDependenciesByName, "RuntimeReferenceOnly", "true");

            // Gather the ResolvedItems from the SDKReference where the reference was resolved.
            ResolvedSDKReferences = sdkReferenceItems.Where(x => x.Resolved).Select(x => x.ResolvedItem).ToArray<ITaskItem>();

            VerifySDKDependsOn(Log, sdkReferenceItems);

            return !Log.HasLoggedErrors;
        }

        /// <summary>
        /// Add metadata to a specified subset of reference items
        /// </summary>
        internal static void AddMetadataToReferences(TaskLoggingHelper log, HashSet<SDKReference> sdkReferenceItems, Dictionary<string, string> referencesToAddMetadata, string metadataName, string metadataValue)
        {
            if (referencesToAddMetadata != null)
            {
                foreach (SDKReference referenceItem in sdkReferenceItems)
                {
                    string sdkSimpleName = referenceItem.SimpleName;
                    string rawSdkVersion = referenceItem.Version;

                    if (referencesToAddMetadata.ContainsKey(sdkSimpleName) && referencesToAddMetadata[sdkSimpleName].Equals(rawSdkVersion, StringComparison.InvariantCultureIgnoreCase))
                    {
                        referenceItem.ResolvedItem.SetMetadata(metadataName, metadataValue);
                    }
                }
            }
        }

        /// <summary>
        /// Verify the dependencies SDKs have for each other
        /// </summary>
        internal static void VerifySDKDependsOn(TaskLoggingHelper log, HashSet<SDKReference> sdkReferenceItems)
        {
            foreach (SDKReference reference in sdkReferenceItems)
            {
                List<string> dependentSDKs = ParseDependsOnSDK(reference.DependsOnSDK);
                if (dependentSDKs.Count > 0)
                {
                    // Get the list of dependencies that are not resolved and are depended on by the current sdk
                    string[] unresolvedDependencyIdentities = GetUnresolvedDependentSDKs(sdkReferenceItems, dependentSDKs);

                    // Generate the string of sdks which were not resolved and are depended upon by this sdk
                    string missingDependencies = String.Join(CommaSpaceDelimiter, unresolvedDependencyIdentities);

                    if (missingDependencies.Length > 0)
                    {
                        log.LogWarningWithCodeFromResources("ResolveSDKReference.SDKMissingDependency", reference.SDKName, missingDependencies);
                    }
                }
            }
        }

        /// <summary>
        /// Get a set of unresolved SDK identities
        /// </summary>
        internal static string[] GetUnresolvedDependentSDKs(HashSet<SDKReference> sdkReferenceItems, List<string> dependentSDKs)
        {
            string[] unresolvedDependencyIdentities = dependentSDKs.Where(x =>
            {
                bool parseSuccessful = ParseSDKReference(x, out string simpleName, out string sdkVersion);
                if (!parseSuccessful)
                {
                    // If a dependency could not be parsed as an SDK identity then ignore it from the list of unresolved dependencies
                    return false;
                }

                // See if there are any resolved references that have the correct simple name and version
                var resolvedReference = sdkReferenceItems.Where(y => String.Equals(y.SimpleName, simpleName, StringComparison.OrdinalIgnoreCase) && String.Equals(y.Version, sdkVersion, StringComparison.OrdinalIgnoreCase)).DefaultIfEmpty(null).FirstOrDefault();

                // Return true if no reference could be found
                return resolvedReference == null;
            })
            .Select(y => String.Format(CultureInfo.CurrentCulture, "\"{0}\"", y))
            .ToArray();

            return unresolvedDependencyIdentities;
        }

        /// <summary>
        /// Parse out the sdk identities
        /// </summary>
        internal static List<string> ParseDependsOnSDK(string dependsOnSDK)
        {
            if (!String.IsNullOrEmpty(dependsOnSDK))
            {
                return dependsOnSDK.Split(s_semicolonSplitChar, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(y => y.Length > 0).ToList<string>();
            }

            return new List<string>();
        }

        /// <summary>
        /// Parse the item include of the SDKReference item into its simple name and version parts.
        /// </summary>
        internal SDKReference ParseSDKReference(ITaskItem referenceItem)
        {
            bool splitSuccessful = ParseSDKReference(referenceItem.ItemSpec, out string sdkSimpleName, out string rawSdkVersion);

            if (!splitSuccessful)
            {
                LogErrorOrWarning(new Tuple<string, object[]>("ResolveSDKReference.SDKReferenceIncorrectFormat", new object[] { referenceItem.ItemSpec }));
                return null;
            }

            var reference = new SDKReference(referenceItem, sdkSimpleName, rawSdkVersion);
            return reference;
        }

        /// <summary>
        /// Take the identity of an sdk and use a regex to parse out the version and simple name
        /// </summary>
        private static bool ParseSDKReference(string reference, out string sdkSimpleName, out string rawSdkVersion)
        {
            Match match = s_sdkReferenceFormat.Match(reference);

            sdkSimpleName = String.Empty;
            bool parsedVersion = false;
            rawSdkVersion = String.Empty;

            if (match.Success)
            {
                sdkSimpleName = match.Groups[SDKsimpleNameGroup].Value.Trim();

                rawSdkVersion = match.Groups[SDKVersionGroup].Value.Trim();
                parsedVersion = Version.TryParse(rawSdkVersion, out _);
            }

            return sdkSimpleName.Length > 0 && parsedVersion;
        }

        /// <summary>
        /// Log where we searched ect, for sdk references and if we found them or not.
        /// </summary>
        private void LogResolution(SDKReference reference)
        {
            Log.LogMessageFromResources(MessageImportance.Low, "ResolveSDKReference.SearchingForSDK", reference.ReferenceItem.ItemSpec);

            if (reference.Resolved)
            {
                Log.LogMessageFromResources(MessageImportance.Low, "ResolveSDKReference.FoundSDK", reference.ResolvedPath);
                if (reference.StatusMessages != null)
                {
                    foreach (Tuple<string, object[]> message in reference.StatusMessages)
                    {
                        string formattedMessage = ResourceUtilities.FormatResourceString(message.Item1, message.Item2);
                        Log.LogMessage("  " + formattedMessage);
                    }
                }
            }
            else if (reference.ResolutionErrors == null || reference.ResolutionErrors.Count == 0)
            {
                // We only want to say we could not find it if there were no other errors which would cause it not to be found
                LogErrorOrWarning(new Tuple<string, object[]>("ResolveSDKReference.CouldNotResolveSDK", new object[] { reference.ReferenceItem.ItemSpec }));
            }

            // Log warnings
            if (reference.ResolutionWarnings != null)
            {
                foreach (Tuple<string, object[]> warning in reference.ResolutionWarnings)
                {
                    Log.LogWarningWithCodeFromResources(warning.Item1, warning.Item2);
                }
            }

            // Log errors
            if (reference.ResolutionErrors != null)
            {
                foreach (Tuple<string, object[]> error in reference.ResolutionErrors)
                {
                    LogErrorOrWarning(error);
                }
            }
        }

        /// <summary>
        /// Log an error or warning depending on the LogErrorsAsWarnigns propertry.
        /// </summary>
        private void LogErrorOrWarning(Tuple<string, object[]> errorOrWarning)
        {
            if (LogResolutionErrorsAsWarnings)
            {
                Log.LogWarningWithCodeFromResources(errorOrWarning.Item1, errorOrWarning.Item2);
            }
            else
            {
                Log.LogErrorWithCodeFromResources(errorOrWarning.Item1, errorOrWarning.Item2);
            }
        }

        /// <summary>
        /// This class holds the sdk reference task item and the split versions of the simple name and version.
        /// </summary>
        internal class SDKReference : IEquatable<SDKReference>
        {
            /// <summary>
            /// Delimiter for supported architectures
            /// </summary>
            private static readonly char[] s_supportedArchitecturesSplitChars = { ';' };

            /// <summary>
            /// Delimiter used to delimit the supported architectures in the error message
            /// </summary>
            private const string SupportedArchitectureJoinDelimiter = ", ";

            /// <summary>
            /// Neutral architecture name
            /// </summary>
            private const string NeutralArch = "Neutral";

            /// <summary>
            /// Neutral architecture name
            /// </summary>
            private const string X64Arch = "X64";

            /// <summary>
            /// X86 architecture name
            /// </summary>
            private const string X86Arch = "X86";

            /// <summary>
            /// ARM architecture name
            /// </summary>
            private const string ARMArch = "ARM";

            /// <summary>
            /// ANY CPU architecture name
            /// </summary>
            private const string AnyCPUArch = "Any CPU";

            /// <summary>
            /// TargetedSDKArchitecture metadata name
            /// </summary>
            private const string TargetedSDKArchitecture = "TargetedSDKArchitecture";

            /// <summary>
            /// TargetedSDKConfiguration metadata name
            /// </summary>
            private const string TargetedSDKConfiguration = "TargetedSDKConfiguration";

            /// <summary>
            /// Retail config name
            /// </summary>
            private const string Retail = "Retail";

            /// <summary>
            /// Debug config name
            /// </summary>
            private const string Debug = "Debug";

            /// <summary>
            /// Path to the sdk manifest file
            /// </summary>
            private string _sdkManifestPath = String.Empty;

            /// <summary>
            /// SDKManifest object encapsulating all the information contained in the manifest xml file
            /// </summary>
            private SDKManifest _sdkManifest;

            /// <summary>
            /// What should happen if this sdk is resolved with other sdks of the same productfamily or same sdk name.
            /// </summary>
            private MultipleVersionSupport _supportsMultipleVersions;

            /// <summary>
            /// Value of the prefer32Bit property from the project.
            /// </summary>
            private bool _prefer32BitFromProject;

            #region Constructor
            /// <summary>
            /// Constructor
            /// </summary>
            public SDKReference(ITaskItem taskItem, string sdkName, string sdkVersion)
            {
                ErrorUtilities.VerifyThrowArgumentNull(taskItem, nameof(taskItem));
                ErrorUtilities.VerifyThrowArgumentLength(sdkName, nameof(sdkName));
                ErrorUtilities.VerifyThrowArgumentLength(sdkVersion, nameof(sdkVersion));

                ReferenceItem = taskItem;
                SimpleName = sdkName;
                Version = sdkVersion;
                SDKName = String.Format(CultureInfo.InvariantCulture, "{0}, Version={1}", SimpleName, Version);
                FrameworkIdentitiesFromManifest = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                AppxLocationsFromManifest = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                ResolutionErrors = new List<Tuple<string, object[]>>();
                ResolutionWarnings = new List<Tuple<string, object[]>>();
                StatusMessages = new List<Tuple<string, object[]>>();
                _supportsMultipleVersions = MultipleVersionSupport.Allow;
            }
            #endregion

            #region Properties
            /// <summary>
            ///  Sdk reference item passed in from the build
            /// </summary>
            public ITaskItem ReferenceItem { get; }

            /// <summary>
            /// Parsed simple name
            /// </summary>
            public string SimpleName { get; }

            /// <summary>
            /// Parsed version.
            /// </summary>
            public string Version { get; }

            /// <summary>
            /// Resolved full path to the root of the sdk.
            /// </summary>
            public string ResolvedPath { get; private set; }

            /// <summary>
            /// Has the reference been resolved
            /// </summary>
            public bool Resolved => !String.IsNullOrEmpty(ResolvedPath);

            /// <summary>
            /// Messages which may be warnings or errors depending on the logging setting.
            /// </summary>
            public List<Tuple<string, object[]>> ResolutionErrors { get; }

            /// <summary>
            /// Warning messages only
            /// </summary>
            public List<Tuple<string, object[]>> ResolutionWarnings { get; }

            /// <summary>
            /// Messages generated during resolution
            /// </summary>
            public List<Tuple<string, object[]>> StatusMessages { get; }

            /// <summary>
            /// SDKName, this is a formatted name based on the SimpleName and the Version
            /// </summary>
            public string SDKName { get; }

            /// <summary>
            /// Resolved item which will be output by the task.
            /// </summary>
            public ITaskItem ResolvedItem { get; set; }

            /// <summary>
            /// SDKType found in the sdk manifest
            /// </summary>
            public SDKType SDKType { get; set; }

            /// <summary>
            /// The target platform in the sdk manifest
            /// </summary>
            public string TargetPlatform { get; set; }

            /// <summary>
            /// The target platform min version in the sdk manifest
            /// </summary>
            public string TargetPlatformMinVersion { get; set; }

            /// <summary>
            /// The target platform max version in the sdk manifest
            /// </summary>
            public string TargetPlatformVersion { get; set; }

            /// <summary>
            /// DisplayName found in the sdk manifest
            /// </summary>
            public string DisplayName { get; set; }

            /// <summary>
            /// Support Prefer32bit found in the sdk manifest
            /// </summary>
            public string SupportPrefer32Bit { get; set; }

            /// <summary>
            /// CopyRedistToSubDirectory specifies where the redist files should be copied to relative to the root of the package.
            /// </summary>
            public string CopyRedistToSubDirectory { get; set; }

            /// <summary>
            /// ProductFamilyName specifies the product family for the SDK. This is offered up as metadata on the resolved sdkreference and is used to detect sdk conflicts.
            /// </summary>
            public string ProductFamilyName { get; set; }

            /// <summary>
            /// SupportsMultipleVersions specifies what should happen if multiple versions of the product family or sdk name are detected
            /// </summary>
            public MultipleVersionSupport SupportsMultipleVersions
            {
                get => _supportsMultipleVersions;
                set => _supportsMultipleVersions = value;
            }

            /// <summary>
            /// Supported Architectures is a semicolon delimited list of architectures that the SDK supports.
            /// </summary>
            public string SupportedArchitectures { get; set; }

            /// <summary>
            /// DependsOnSDK is a semicolon delimited list of SDK identities that the SDK requires be resolved in order to function.
            /// </summary>
            public string DependsOnSDK { get; set; }

            /// <summary>
            /// MaxPlatformVersion as in the manifest
            /// </summary>
            public string MaxPlatformVersion { get; set; }

            /// <summary>
            /// MinOSVersion as in the manifest
            /// </summary>
            public string MinOSVersion { get; set; }

            /// <summary>
            /// MaxOSVersionTested as in the manifest
            /// </summary>
            public string MaxOSVersionTested { get; set; }

            /// <summary>
            /// MoreInfo as in the manifest
            /// </summary>
            public string MoreInfo { get; set; }

            /// <summary>
            /// What ever framework identities we found in the manifest.
            /// </summary>
            private Dictionary<string, string> FrameworkIdentitiesFromManifest { get; }

            /// <summary>
            /// The frameworkIdentity for the sdk, this may be a single name or a | delimited name
            /// </summary>
            private string FrameworkIdentity { get; set; }

            /// <summary>
            /// PlatformIdentity if it exists in the appx manifest for this sdk.
            /// </summary>
            private string PlatformIdentity { get; set; }

            /// <summary>
            /// Whatever appx locations we found in the manifest
            /// </summary>
            private Dictionary<string, string> AppxLocationsFromManifest { get; }

            /// <summary>
            /// The appxlocation for the sdk can be a single name or a | delimited list
            /// </summary>
            private string AppxLocation { get; set; }

            #endregion

            #region Methods

            /// <summary>
            /// Set the location where the reference was resolved.
            /// </summary>
            public void Resolve(Dictionary<string, ITaskItem> sdks, string targetConfiguration, string targetArchitecture, HashSet<string> sdkNamesOnReferenceItems, bool treatErrorsAsWarnings, bool prefer32Bit, string identifierTargetPlatform, Version versionTargetPlatform, string projectName, bool enableMaxPlatformVersionEmptyWarning)
            {
                if (sdks.ContainsKey(SDKName))
                {
                    _prefer32BitFromProject = prefer32Bit;

                    // There must be a trailing slash or else the ExpandSDKReferenceAssemblies will not work.
                    ResolvedPath = FileUtilities.EnsureTrailingSlash(sdks[SDKName].ItemSpec);

                    System.Version.TryParse(sdks[SDKName].GetMetadata(SDKPlatformVersion), out Version targetPlatformVersionFromItem);

                    GetSDKManifestAttributes();

                    CreateResolvedReferenceItem(targetConfiguration, targetArchitecture, sdkNamesOnReferenceItems, identifierTargetPlatform, versionTargetPlatform, targetPlatformVersionFromItem, projectName, enableMaxPlatformVersionEmptyWarning);

                    // Need to pass these along so we can unroll the platform via GetMatchingPlatformSDK when we get reference files
                    ResolvedItem.SetMetadata(GetInstalledSDKLocations.DirectoryRootsMetadataName, sdks[SDKName].GetMetadata(GetInstalledSDKLocations.DirectoryRootsMetadataName));
                    ResolvedItem.SetMetadata(GetInstalledSDKLocations.ExtensionDirectoryRootsMetadataName, sdks[SDKName].GetMetadata(GetInstalledSDKLocations.ExtensionDirectoryRootsMetadataName));
                    ResolvedItem.SetMetadata(GetInstalledSDKLocations.RegistryRootMetadataName, sdks[SDKName].GetMetadata(GetInstalledSDKLocations.RegistryRootMetadataName));

                    if (!treatErrorsAsWarnings && ResolutionErrors.Count > 0)
                    {
                        ResolvedPath = String.Empty;
                    }
                }
            }

            /// <summary>
            /// Override object equals to use the equals implementation in this object.
            /// </summary>
            public override bool Equals(object obj)
            {
                if (!(obj is SDKReference reference))
                {
                    return false;
                }

                return Equals(reference);
            }

            /// <summary>
            /// Override get hash code
            /// </summary>
            public override int GetHashCode()
            {
                return SimpleName.GetHashCode() ^ Version.GetHashCode();
            }

            /// <summary>
            /// Are two SDKReference items Equal
            /// </summary>
            public bool Equals(SDKReference other)
            {
                if (other == null)
                {
                    return false;
                }

                if (Object.ReferenceEquals(other, this))
                {
                    return true;
                }

                bool simpleNameMatches = String.Equals(SimpleName, other.SimpleName, StringComparison.OrdinalIgnoreCase);
                bool versionMatches = Version.Equals(other.Version, StringComparison.OrdinalIgnoreCase);

                return simpleNameMatches && versionMatches;
            }

            /// <summary>
            /// Add a resolution error or warning to the reference
            /// </summary>
            internal void AddResolutionErrorOrWarning(string resourceId, params object[] parameters)
            {
                ResolutionErrors.Add(new Tuple<string, object[]>(resourceId, parameters));
            }

            /// <summary>
            /// Add a resolution warning to the reference
            /// </summary>
            internal void AddResolutionWarning(string resourceId, params object[] parameters)
            {
                ResolutionWarnings.Add(new Tuple<string, object[]>(resourceId, parameters));
            }

            /// <summary>
            /// Get a piece of metadata off an item and make sureit is trimmed
            /// </summary>
            private static string GetItemMetadataTrimmed(ITaskItem item, string metadataName)
            {
                string metadataValue = item.GetMetadata(metadataName);
                return metadataValue?.Trim();
            }

            /// <summary>
            /// After resolving a reference we need to check to see if there is a SDKManifest file in the root directory and if there is we need to extract the frameworkidentity.
            /// We ignore other attributes to leave room for expansion of the file format.
            /// 
            /// </summary>
            private void GetSDKManifestAttributes()
            {
                if (_sdkManifest == null)
                {
                    _sdkManifestPath = Path.Combine(ResolvedPath, "SDKManifest.xml");

                    AddStatusMessage("ResolveSDKReference.ReadingSDKManifestFile", _sdkManifestPath);

                    _sdkManifest = new SDKManifest(ResolvedPath);

                    if (_sdkManifest.ReadError)
                    {
                        AddResolutionErrorOrWarning("ResolveSDKReference.ErrorResolvingSDK", ReferenceItem.ItemSpec, ResourceUtilities.FormatResourceString("ResolveSDKReference.ErrorReadingManifest", _sdkManifestPath, _sdkManifest.ReadErrorMessage));
                    }
                }

                SupportedArchitectures = GetItemMetadataTrimmed(ReferenceItem, SDKManifest.Attributes.SupportedArchitectures);
                if (String.IsNullOrEmpty(SupportedArchitectures))
                {
                    SupportedArchitectures = _sdkManifest.SupportedArchitectures ?? String.Empty;
                }

                DependsOnSDK = GetItemMetadataTrimmed(ReferenceItem, SDKManifest.Attributes.DependsOnSDK);
                if (String.IsNullOrEmpty(DependsOnSDK))
                {
                    DependsOnSDK = _sdkManifest.DependsOnSDK ?? String.Empty;
                }

                FrameworkIdentity = GetItemMetadataTrimmed(ReferenceItem, SDKManifest.Attributes.FrameworkIdentity);
                if (String.IsNullOrEmpty(FrameworkIdentity))
                {
                    if (_sdkManifest.FrameworkIdentities != null)
                    {
                        foreach (string key in _sdkManifest.FrameworkIdentities.Keys)
                        {
                            if (!FrameworkIdentitiesFromManifest.ContainsKey(key))
                            {
                                FrameworkIdentitiesFromManifest.Add(key, _sdkManifest.FrameworkIdentities[key]);
                            }
                        }
                    }

                    FrameworkIdentity = _sdkManifest.FrameworkIdentity ?? String.Empty;
                }

                AppxLocation = GetItemMetadataTrimmed(ReferenceItem, SDKManifest.Attributes.AppxLocation);
                if (String.IsNullOrEmpty(AppxLocation))
                {
                    if (_sdkManifest.AppxLocations != null)
                    {
                        foreach (string key in _sdkManifest.AppxLocations.Keys)
                        {
                            if (!AppxLocationsFromManifest.ContainsKey(key))
                            {
                                AppxLocationsFromManifest.Add(key, _sdkManifest.AppxLocations[key]);
                            }
                        }
                    }
                }

                PlatformIdentity = GetItemMetadataTrimmed(ReferenceItem, SDKManifest.Attributes.PlatformIdentity);
                if (String.IsNullOrEmpty(PlatformIdentity))
                {
                    PlatformIdentity = _sdkManifest.PlatformIdentity ?? String.Empty;
                }

                MinOSVersion = GetItemMetadataTrimmed(ReferenceItem, SDKManifest.Attributes.MinOSVersion);
                if (String.IsNullOrEmpty(MinOSVersion))
                {
                    MinOSVersion = _sdkManifest.MinOSVersion ?? String.Empty;
                }

                MaxOSVersionTested = GetItemMetadataTrimmed(ReferenceItem, SDKManifest.Attributes.MaxOSVersionTested);
                if (String.IsNullOrEmpty(MaxOSVersionTested))
                {
                    MaxOSVersionTested = _sdkManifest.MaxOSVersionTested ?? String.Empty;
                }

                MoreInfo = GetItemMetadataTrimmed(ReferenceItem, SDKManifest.Attributes.MoreInfo);
                if (String.IsNullOrEmpty(MoreInfo))
                {
                    MoreInfo = _sdkManifest.MoreInfo ?? String.Empty;
                }

                MaxPlatformVersion = GetItemMetadataTrimmed(ReferenceItem, SDKManifest.Attributes.MaxPlatformVersion);
                if (String.IsNullOrEmpty(MaxPlatformVersion))
                {
                    MaxPlatformVersion = _sdkManifest.MaxPlatformVersion ?? String.Empty;
                }

                TargetPlatform = GetItemMetadataTrimmed(ReferenceItem, SDKManifest.Attributes.TargetPlatform);
                if (String.IsNullOrEmpty(TargetPlatform))
                {
                    TargetPlatform = _sdkManifest.TargetPlatform ?? String.Empty;
                }

                TargetPlatformMinVersion = GetItemMetadataTrimmed(ReferenceItem, SDKManifest.Attributes.TargetPlatformMinVersion);
                if (String.IsNullOrEmpty(TargetPlatformMinVersion))
                {
                    TargetPlatformMinVersion = _sdkManifest.TargetPlatformMinVersion ?? String.Empty;
                }

                TargetPlatformVersion = GetItemMetadataTrimmed(ReferenceItem, SDKManifest.Attributes.TargetPlatformVersion);
                if (String.IsNullOrEmpty(TargetPlatformVersion))
                {
                    TargetPlatformVersion = _sdkManifest.TargetPlatformVersion ?? String.Empty;
                }

                string sdkTypeFromMetadata = GetItemMetadataTrimmed(ReferenceItem, SDKManifest.Attributes.SDKType);
                if (String.IsNullOrEmpty(sdkTypeFromMetadata))
                {
                    SDKType = _sdkManifest.SDKType;
                }
                else
                {
                    Enum.TryParse<SDKType>(sdkTypeFromMetadata, out SDKType sdkType);
                    SDKType = sdkType;
                }

                DisplayName = GetItemMetadataTrimmed(ReferenceItem, SDKManifest.Attributes.DisplayName);
                if (String.IsNullOrEmpty(DisplayName))
                {
                    DisplayName = _sdkManifest.DisplayName ?? String.Empty;
                }

                SupportPrefer32Bit = GetItemMetadataTrimmed(ReferenceItem, SDKManifest.Attributes.SupportPrefer32Bit);
                if (String.IsNullOrEmpty(SupportPrefer32Bit))
                {
                    SupportPrefer32Bit = _sdkManifest.SupportPrefer32Bit ?? String.Empty;
                }

                CopyRedistToSubDirectory = GetItemMetadataTrimmed(ReferenceItem, SDKManifest.Attributes.CopyRedistToSubDirectory);
                if (String.IsNullOrEmpty(CopyRedistToSubDirectory))
                {
                    CopyRedistToSubDirectory = _sdkManifest.CopyRedistToSubDirectory ?? String.Empty;
                }

                ProductFamilyName = GetItemMetadataTrimmed(ReferenceItem, SDKManifest.Attributes.ProductFamilyName);
                if (String.IsNullOrEmpty(ProductFamilyName))
                {
                    ProductFamilyName = _sdkManifest.ProductFamilyName ?? String.Empty;
                }

                if (!ParseSupportMultipleVersions(GetItemMetadataTrimmed(ReferenceItem, SDKManifest.Attributes.SupportsMultipleVersions)))
                {
                    _supportsMultipleVersions = _sdkManifest.SupportsMultipleVersions;
                }
            }

            /// <summary>
            /// Parse the multipleversions string and set supportsMultipleVersions if it can be parsed correctly.
            /// </summary>
            private bool ParseSupportMultipleVersions(string multipleVersionsValue)
            {
                return !String.IsNullOrEmpty(multipleVersionsValue) && Enum.TryParse<MultipleVersionSupport>(multipleVersionsValue, /*ignoreCase*/true, out _supportsMultipleVersions);
            }

            /// <summary>
            /// Create a resolved output item which contains the path to the SDK and the associated metadata about it.
            /// </summary>
            private void CreateResolvedReferenceItem(string targetConfiguration, string targetArchitecture, HashSet<string> sdkNamesOnReferenceItems, string targetPlatformIdentifier, Version targetPlatformVersion, Version targetPlatformVersionFromItem, string projectName, bool enableMaxPlatformVersionEmptyWarning)
            {
                // Make output item to send to the project file which represents a resolve SDKReference
                ResolvedItem = new TaskItem(ResolvedPath);
                ResolvedItem.SetMetadata("SDKName", SDKName);

                if (!String.IsNullOrEmpty(ProductFamilyName))
                {
                    ResolvedItem.SetMetadata(SDKManifest.Attributes.ProductFamilyName, ProductFamilyName);
                }

                // Copy existing metadata onto the output item
                ReferenceItem.CopyMetadataTo(ResolvedItem);

                ResolvedItem.SetMetadata("SupportsMultipleVersions", _supportsMultipleVersions.ToString());

                // If no architecture and configuration is passed in then default to retail neutral
                targetArchitecture = String.IsNullOrEmpty(targetArchitecture) ? NeutralArch : targetArchitecture;
                targetConfiguration = String.IsNullOrEmpty(targetConfiguration) ? Retail : targetConfiguration;

                // Check to see if there was metadata on the original reference item, if there is then that wins.
                string sdkConfiguration = ReferenceItem.GetMetadata(TargetedSDKConfiguration);
                sdkConfiguration = sdkConfiguration.Length > 0 ? sdkConfiguration : targetConfiguration;

                string sdkArchitecture = ReferenceItem.GetMetadata(TargetedSDKArchitecture).Length > 0 ? ReferenceItem.GetMetadata(TargetedSDKArchitecture) : targetArchitecture;
                sdkArchitecture = sdkArchitecture.Length > 0 ? sdkArchitecture : targetArchitecture;

                // Configuration is somewhat special, if Release is passed in me want to convert it to Retail and set that on the resulting output item.
                sdkConfiguration = sdkConfiguration.Equals("Release", StringComparison.OrdinalIgnoreCase) ? Retail : sdkConfiguration;

                sdkArchitecture = sdkArchitecture.Equals("msil", StringComparison.OrdinalIgnoreCase) ? NeutralArch : sdkArchitecture;
                sdkArchitecture = sdkArchitecture.Equals("AnyCPU", StringComparison.OrdinalIgnoreCase) ? NeutralArch : sdkArchitecture;
                sdkArchitecture = sdkArchitecture.Equals("Any CPU", StringComparison.OrdinalIgnoreCase) ? NeutralArch : sdkArchitecture;
                sdkArchitecture = sdkArchitecture.Equals("amd64", StringComparison.OrdinalIgnoreCase) ? X64Arch : sdkArchitecture;

                ResolvedItem.SetMetadata(TargetedSDKConfiguration, sdkConfiguration);
                ResolvedItem.SetMetadata(TargetedSDKArchitecture, sdkArchitecture);

                // Print out a message indicating what our targeted sdk configuration and architecture is so users know what the reference is targeting.
                AddStatusMessage("ResolveSDKReference.TargetedConfigAndArchitecture", sdkConfiguration, sdkArchitecture);

                string[] supportedArchitectures = null;
                if (!string.IsNullOrEmpty(SupportedArchitectures))
                {
                    supportedArchitectures = SupportedArchitectures.Split(s_supportedArchitecturesSplitChars, StringSplitOptions.RemoveEmptyEntries);
                }

                if (supportedArchitectures != null)
                {
                    bool foundTargetArchitecture = false;

                    // SupportedArchitectures will usually only contain a handful of elements therefore putting this into a hashtable or dictionary would not likely give us much performance improvement.
                    foreach (string architecture in supportedArchitectures)
                    {
                        if (architecture.Equals(sdkArchitecture, StringComparison.OrdinalIgnoreCase))
                        {
                            foundTargetArchitecture = true;
                            break;
                        }
                    }

                    if (!foundTargetArchitecture)
                    {
                        string remappedArchitecture = sdkArchitecture.Equals(NeutralArch, StringComparison.OrdinalIgnoreCase) ? AnyCPUArch : sdkArchitecture;
                        string supportedArchList = String.Empty;

                        for (int i = 0; i < supportedArchitectures.Length; i++)
                        {
                            supportedArchList += supportedArchitectures[i].Equals(NeutralArch, StringComparison.OrdinalIgnoreCase) ? AnyCPUArch : supportedArchitectures[i];

                            // only put a comma after the first if there is more that one and do not put one after the end
                            if (supportedArchitectures.Length > 1 && i != supportedArchitectures.Length - 1)
                            {
                                supportedArchList += SupportedArchitectureJoinDelimiter;
                            }
                        }

                        AddResolutionErrorOrWarning("ResolveSDKReference.TargetArchitectureNotSupported", remappedArchitecture, SDKName, supportedArchList);
                    }
                }

                if (!String.IsNullOrEmpty(MaxPlatformVersion))
                {
                    if (System.Version.TryParse(MaxPlatformVersion, out Version maxPlatformVersionAsVersion) && (maxPlatformVersionAsVersion < targetPlatformVersion))
                    {
                        AddResolutionWarning("ResolveSDKReference.MaxPlatformVersionLessThanTargetPlatformVersion", projectName, DisplayName, Version, targetPlatformIdentifier, MaxPlatformVersion, targetPlatformIdentifier, targetPlatformVersion.ToString());
                    }
                }
                else if (enableMaxPlatformVersionEmptyWarning && targetPlatformVersionFromItem != null && targetPlatformVersionFromItem < targetPlatformVersion)
                {
                    AddResolutionWarning("ResolveSDKReference.MaxPlatformVersionNotSpecified", projectName, DisplayName, Version, targetPlatformIdentifier, targetPlatformVersionFromItem.ToString(), targetPlatformIdentifier, targetPlatformVersion.ToString());
                }

                if (!String.IsNullOrEmpty(TargetPlatform) && !String.Equals(targetPlatformIdentifier, TargetPlatform))
                {
                    AddResolutionErrorOrWarning("ResolveSDKReference.TargetPlatformIdentifierDoesNotMatch", projectName, DisplayName, Version, targetPlatformIdentifier, TargetPlatform);
                }

                if (!String.IsNullOrEmpty(TargetPlatformMinVersion))
                {
                    if (System.Version.TryParse(TargetPlatformMinVersion, out Version targetPlatformMinVersionAsVersion) && (targetPlatformVersion < targetPlatformMinVersionAsVersion))
                    {
                        AddResolutionErrorOrWarning("ResolveSDKReference.PlatformVersionIsLessThanMinVersion", projectName, DisplayName, Version, targetPlatformVersion.ToString(), targetPlatformMinVersionAsVersion.ToString());
                    }
                }

                if (String.Equals(NeutralArch, sdkArchitecture, StringComparison.OrdinalIgnoreCase) && !String.IsNullOrEmpty(SupportPrefer32Bit) && _prefer32BitFromProject)
                {
                    bool.TryParse(SupportPrefer32Bit, out bool supportPrefer32Bit);

                    if (!supportPrefer32Bit)
                    {
                        AddResolutionErrorOrWarning("ResolveSDKReference.Prefer32BitNotSupportedWithNeutralProject", SDKName);
                    }
                }

                // The SDKManifest may have had a number of frameworkidentity entries inside of it. We want to match the one
                // which has the correct configuration and architecture. If a perfect match cannot be found 
                // then we will look for ones that declare only the configuration. If that cannot be found we just try and find an element that only is "FrameworkIdentity".
                if (String.IsNullOrEmpty(FrameworkIdentity))
                {
                    if (FrameworkIdentitiesFromManifest.Count > 0)
                    {
                        // Try and find a framework identity that matches on both the configuration and architecture "FrameworkIdentity-<Config>-<Arch>"
                        FrameworkIdentity = null;
                        string frameworkIdentityKey = String.Format(CultureInfo.InvariantCulture, "{0}-{1}-{2}", SDKManifest.Attributes.FrameworkIdentity, sdkConfiguration, sdkArchitecture);
                        FrameworkIdentity = FindFrameworkIdentity(frameworkIdentityKey);

                        // Try and find a framework identity that matches on the configuration , Element must be named "FrameworkIdentity-<Config>" only.
                        if (FrameworkIdentity == null)
                        {
                            frameworkIdentityKey = String.Format(CultureInfo.InvariantCulture, "{0}-{1}", SDKManifest.Attributes.FrameworkIdentity, sdkConfiguration);
                            FrameworkIdentity = FindFrameworkIdentity(frameworkIdentityKey);
                        }

                        // See if there is an element just called "FrameworkIdentity"
                        if (FrameworkIdentity == null)
                        {
                            frameworkIdentityKey = SDKManifest.Attributes.FrameworkIdentity;
                            FrameworkIdentity = FindFrameworkIdentity(frameworkIdentityKey);
                        }

                        if (FrameworkIdentity == null)
                        {
                            AddResolutionErrorOrWarning("ResolveSDKReference.NoMatchingFrameworkIdentity", _sdkManifestPath, sdkConfiguration, sdkArchitecture);
                        }
                        else
                        {
                            ResolvedItem.SetMetadata(SDKManifest.Attributes.FrameworkIdentity, FrameworkIdentity);
                        }
                    }
                    else
                    {
                        AddStatusMessage("ResolveSDKReference.NoFrameworkIdentitiesFound");
                    }
                }
                else
                {
                    AddStatusMessage("ResolveSDKReference.FoundFrameworkIdentity", FrameworkIdentity);
                }

                // Print out if we are a platform SDK
                if (!String.IsNullOrEmpty(PlatformIdentity))
                {
                    AddStatusMessage("ResolveSDKReference.PlatformSDK", PlatformIdentity);
                    ResolvedItem.SetMetadata(SDKManifest.Attributes.PlatformIdentity, PlatformIdentity);
                }

                // The SDKManifest may have had a number of AppxLocation entries inside of it. We want to return the set of unique architectures for a selected configuration.
                if (String.IsNullOrEmpty(AppxLocation))
                {
                    if (AppxLocationsFromManifest.Count > 0)
                    {
                        AppxLocation = null;

                        // For testing especially it's nice to have a set order of what the generated appxlocation string will be at the end
                        var architectureLocations = new SortedDictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
                        List<string> appxLocationComponents = new List<string>();

                        foreach (var appxLocation in AppxLocationsFromManifest)
                        {
                            if (!String.IsNullOrEmpty(appxLocation.Key))
                            {
                                string[] appxComponents = appxLocation.Key.Split(s_appxSplitChar, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToArray();

                                // The first component needs to be appx
                                if (!String.Equals("Appx", appxComponents[0], StringComparison.OrdinalIgnoreCase))
                                {
                                    continue;
                                }

                                string configurationComponent = null;
                                string architectureComponent;
                                switch (appxComponents.Length)
                                {
                                    case 1:
                                        architectureComponent = NeutralArch;
                                        break;
                                    case 2:
                                        configurationComponent = appxComponents[1];
                                        architectureComponent = NeutralArch;

                                        // If the configuration is not debug or retail then we will assume it is an architecture
                                        if (!(configurationComponent.Equals(Debug, StringComparison.OrdinalIgnoreCase) || configurationComponent.Equals(Retail, StringComparison.OrdinalIgnoreCase)))
                                        {
                                            configurationComponent = null;
                                            architectureComponent = appxComponents[1];
                                        }

                                        break;
                                    case 3:
                                        configurationComponent = appxComponents[1];
                                        architectureComponent = appxComponents[2];
                                        break;
                                    default:
                                        // Not one of the cases we expect, just skip it
                                        continue;
                                }

                                bool containsKey = architectureLocations.ContainsKey(architectureComponent);

                                // If we have not seen this architecture before (and it has a compatible configuration with what we are targeting) then add it. 
                                // Also, replace the entry if we have already added an entry for a non configuration specific entry and we now have a configuration specific entry that matches what we are targeting.
                                if ((configurationComponent == null && !containsKey) || (configurationComponent != null && configurationComponent.Equals(sdkConfiguration, StringComparison.OrdinalIgnoreCase)))
                                {
                                    AddStatusMessage("ResolveSDKReference.FoundAppxLocation", appxLocation.Key + "=" + appxLocation.Value);

                                    if (containsKey)
                                    {
                                        AddStatusMessage("ResolveSDKReference.ReplaceAppxLocation", architectureComponent, architectureLocations[architectureComponent], appxLocation.Value);
                                    }

                                    architectureLocations[architectureComponent] = appxLocation.Value;
                                }
                            }
                        }

                        foreach (var location in architectureLocations)
                        {
                            appxLocationComponents.Add(location.Key);
                            appxLocationComponents.Add(location.Value);
                        }

                        if (appxLocationComponents.Count > 0)
                        {
                            AppxLocation = String.Join("|", appxLocationComponents.ToArray());
                        }

                        if (AppxLocation == null)
                        {
                            AddResolutionErrorOrWarning("ResolveSDKReference.NoMatchingAppxLocation", _sdkManifestPath, sdkConfiguration, sdkArchitecture);
                        }
                        else
                        {
                            ResolvedItem.SetMetadata(SDKManifest.Attributes.AppxLocation, AppxLocation);
                        }
                    }
                    else
                    {
                        AddStatusMessage("ResolveSDKReference.NoAppxLocationsFound");
                    }
                }
                else
                {
                    AddStatusMessage("ResolveSDKReference.FoundAppxLocation", AppxLocation);
                }

                ResolvedItem.SetMetadata("SimpleName", SimpleName);
                ResolvedItem.SetMetadata("Version", Version);

                // Check to see if the copy local metadata has been set in the project file.
                bool result;
                bool hasExpandReferenceAssemblies = bool.TryParse(ReferenceItem.GetMetadata(SDKManifest.Attributes.ExpandReferenceAssemblies), out result);
                bool hasCopyRedist = bool.TryParse(ReferenceItem.GetMetadata(SDKManifest.Attributes.CopyRedist), out result);
                bool hasCopyLocalExpandedReferenceAssemblies = bool.TryParse(ReferenceItem.GetMetadata(SDKManifest.Attributes.CopyLocalExpandedReferenceAssemblies), out result);

                bool referenceItemHasSDKName = sdkNamesOnReferenceItems.Contains(SDKName);

                if (SDKType != SDKType.Unspecified)
                {
                    ResolvedItem.SetMetadata(SDKManifest.Attributes.SDKType, SDKType.ToString());
                }

                if (!String.IsNullOrEmpty(DisplayName))
                {
                    ResolvedItem.SetMetadata(SDKManifest.Attributes.DisplayName, DisplayName);
                }

                // Could be null or empty depending if blank metadata was set or not.
                bool frameworkSDK = SDKType == SDKType.Framework || !String.IsNullOrEmpty(FrameworkIdentity);
                bool hasPlatformIdentity = SDKType == SDKType.Platform || !String.IsNullOrEmpty(PlatformIdentity);

                if (!hasExpandReferenceAssemblies)
                {
                    if (referenceItemHasSDKName)
                    {
                        ResolvedItem.SetMetadata(SDKManifest.Attributes.ExpandReferenceAssemblies, "false");
                    }
                    else
                    {
                        ResolvedItem.SetMetadata(SDKManifest.Attributes.ExpandReferenceAssemblies, "true");
                    }
                }

                if (!hasCopyRedist)
                {
                    if (frameworkSDK || hasPlatformIdentity)
                    {
                        ResolvedItem.SetMetadata(SDKManifest.Attributes.CopyRedist, "false");
                    }
                    else
                    {
                        ResolvedItem.SetMetadata(SDKManifest.Attributes.CopyRedist, "true");
                    }
                }

                if (!hasCopyLocalExpandedReferenceAssemblies)
                {
                    if (frameworkSDK || referenceItemHasSDKName || hasPlatformIdentity)
                    {
                        ResolvedItem.SetMetadata(SDKManifest.Attributes.CopyLocalExpandedReferenceAssemblies, "false");
                    }
                    else
                    {
                        ResolvedItem.SetMetadata(SDKManifest.Attributes.CopyLocalExpandedReferenceAssemblies, "true");
                    }
                }

                if (!String.IsNullOrEmpty(CopyRedistToSubDirectory))
                {
                    ResolvedItem.SetMetadata(SDKManifest.Attributes.CopyRedistToSubDirectory, CopyRedistToSubDirectory);
                }

                if (!String.IsNullOrEmpty(MaxPlatformVersion))
                {
                    ResolvedItem.SetMetadata(SDKManifest.Attributes.MaxPlatformVersion, MaxPlatformVersion);
                }

                if (!String.IsNullOrEmpty(MinOSVersion))
                {
                    ResolvedItem.SetMetadata(SDKManifest.Attributes.MinOSVersion, MinOSVersion);
                }

                if (!String.IsNullOrEmpty(MaxOSVersionTested))
                {
                    ResolvedItem.SetMetadata(SDKManifest.Attributes.MaxOSVersionTested, MaxOSVersionTested);
                }

                if (!String.IsNullOrEmpty(MoreInfo))
                {
                    ResolvedItem.SetMetadata(SDKManifest.Attributes.MoreInfo, MoreInfo);
                }
            }

            /// <summary>
            /// Check to see if an FrameworkIdentity is in the list of framework identities found in the SDKManifest.
            /// </summary>
            private string FindFrameworkIdentity(string frameworkIdentityKey)
            {
                string frameworkIdentityValue = null;
                if (FrameworkIdentitiesFromManifest.ContainsKey(frameworkIdentityKey))
                {
                    frameworkIdentityValue = FrameworkIdentitiesFromManifest[frameworkIdentityKey];
                    AddStatusMessage("ResolveSDKReference.FoundFrameworkIdentity", frameworkIdentityValue);
                }
                else
                {
                    AddStatusMessage("ResolveSDKReference.CouldNotFindFrameworkIdentity", frameworkIdentityKey);
                }

                return frameworkIdentityValue;
            }

            /// <summary>
            /// Keep track of messages which are status information about resolving this reference. We want to print it out in a nicer format at the end of resolution.
            /// </summary>
            private void AddStatusMessage(string resource, params object[] parameters)
            {
                StatusMessages.Add(new Tuple<string, object[]>(resource, parameters));
            }
            #endregion
        }
    }
}
