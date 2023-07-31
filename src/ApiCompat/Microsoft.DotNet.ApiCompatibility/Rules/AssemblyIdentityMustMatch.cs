// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Logging;
using Microsoft.DotNet.ApiSymbolExtensions.Logging;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    /// <summary>
    /// This rule validates that assembly identities are compatible. The following parameters are considered:
    /// - Assembly exists
    /// - Assembly name (equality)
    /// - Assembly culture (equality)
    /// - Assembly version (compatible)
    /// - Assembly public key token (compatible)
    /// Some checks behave differently in strict mode comparison.
    /// </summary>
    public class AssemblyIdentityMustMatch : IRule
    {
        private readonly ISuppressableLog _log;
        private readonly IRuleSettings _settings;

        public AssemblyIdentityMustMatch(ISuppressableLog log,
            IRuleSettings settings,
            IRuleRegistrationContext context)
        {
            _log = log;
            _settings = settings;
            context.RegisterOnAssemblySymbolAction(RunOnAssemblySymbol);
        }

        private void RunOnAssemblySymbol(IAssemblySymbol? left, IAssemblySymbol? right, MetadataInformation leftMetadata, MetadataInformation rightMetadata, bool singleAssembly, IList<CompatDifference> differences)
        {
            if (left == null && right != null)
            {
                string message = string.Format(Resources.AssemblyNameDoesNotExist, leftMetadata, right.Identity.Name);

                // When operating in strict mode or when comparing a single assembly only, left must not be null.
                if (_settings.StrictMode || singleAssembly)
                {
                    differences.Add(new CompatDifference(
                        leftMetadata,
                        rightMetadata,
                        DiagnosticIds.MatchingAssemblyDoesNotExist,
                        message,
                        DifferenceType.Removed,
                        right.Identity.GetDisplayName()));
                }
                /* When comparing multiple assemblies and not operating in strict mode, we don't emit a difference but an
                   informational message to prevent user errors (i.e. wrong input to the frontend). */
                else
                {
                    _log.LogMessage(MessageImportance.Normal, message);
                }
                return;
            }

            if (left != null && right == null)
            {
                differences.Add(new CompatDifference(
                    leftMetadata,
                    rightMetadata,
                    DiagnosticIds.MatchingAssemblyDoesNotExist,
                    string.Format(Resources.AssemblyNameDoesNotExist, rightMetadata, left.Identity.Name),
                    DifferenceType.Added,
                    left.Identity.GetDisplayName()));
                return;
            }

            // At this point, left and right are both not null.
            AssemblyIdentity leftIdentity = left!.Identity;
            AssemblyIdentity rightIdentity = right!.Identity;

            string leftAssemblyName = leftIdentity.Name;
            string leftAssemblyCulture = string.IsNullOrEmpty(leftIdentity.CultureName) ? "neutral" : leftIdentity.CultureName;
            Version leftAssemblyVersion = leftIdentity.Version;
            ReadOnlySpan<byte> leftAssemblyPublicKeyToken = leftIdentity.PublicKeyToken.AsSpan();

            string rightAssemblyName = rightIdentity.Name;
            string rightAssemblyCulture = string.IsNullOrEmpty(rightIdentity.CultureName) ? "neutral" : rightIdentity.CultureName;
            Version rightAssemblyVersion = rightIdentity.Version;
            ReadOnlySpan<byte> rightAssemblyPublicKeyToken = rightIdentity.PublicKeyToken.AsSpan();

            if (leftAssemblyName != rightAssemblyName)
            {
                differences.Add(CreateIdentityDifference(
                    leftMetadata,
                    rightMetadata,
                    Resources.AssemblyNameDoesNotMatch,
                    leftAssemblyName,
                    rightAssemblyName,
                    leftMetadata.DisplayString,
                    rightMetadata.DisplayString,
                    rightIdentity));
            }

            if (leftAssemblyCulture != rightAssemblyCulture)
            {
                differences.Add(CreateIdentityDifference(
                    leftMetadata,
                    rightMetadata,
                    Resources.AssemblyCultureDoesNotMatch,
                    leftAssemblyCulture,
                    rightAssemblyCulture,
                    leftMetadata.DisplayString,
                    rightMetadata.DisplayString,
                    rightIdentity));
            }
 
            if (rightAssemblyVersion < leftAssemblyVersion)
            {
                differences.Add(CreateIdentityDifference(
                    leftMetadata,
                    rightMetadata,
                    Resources.AssemblyVersionIsNotCompatible,
                    rightAssemblyVersion.ToString(),
                    leftAssemblyVersion.ToString(),
                    rightMetadata.DisplayString,
                    leftMetadata.DisplayString,
                    rightIdentity));
            }
            else if (_settings.StrictMode && leftAssemblyVersion < rightAssemblyVersion)
            {
                differences.Add(CreateIdentityDifference(
                    leftMetadata,
                    rightMetadata,
                    Resources.AssemblyVersionDoesNotMatch,
                    leftAssemblyVersion.ToString(),
                    rightAssemblyVersion.ToString(),
                    leftMetadata.DisplayString,
                    rightMetadata.DisplayString,
                    leftIdentity));
            }

            if (!leftAssemblyPublicKeyToken.IsEmpty && !leftIdentity.IsRetargetable && !leftAssemblyPublicKeyToken.SequenceEqual(rightAssemblyPublicKeyToken))
            {
                differences.Add(CreateIdentityDifference(
                    leftMetadata,
                    rightMetadata,
                    Resources.AssemblyPublicKeyTokenDoesNotMatch,
                    GetStringRepresentation(leftAssemblyPublicKeyToken), 
                    GetStringRepresentation(rightAssemblyPublicKeyToken),
                    leftMetadata.DisplayString,
                    rightMetadata.DisplayString,
                    rightIdentity));
            }
            else if (_settings.StrictMode && !rightAssemblyPublicKeyToken.IsEmpty && !rightIdentity.IsRetargetable && !rightAssemblyPublicKeyToken.SequenceEqual(leftAssemblyPublicKeyToken))
            {
                differences.Add(CreateIdentityDifference(
                    leftMetadata,
                    rightMetadata,
                    Resources.AssemblyPublicKeyTokenDoesNotMatch,
                    GetStringRepresentation(rightAssemblyPublicKeyToken), 
                    GetStringRepresentation(leftAssemblyPublicKeyToken),
                    rightMetadata.DisplayString,
                    leftMetadata.DisplayString,
                    leftIdentity));
            }
        }

        private static string GetStringRepresentation(ReadOnlySpan<byte> publicKeyToken)
        {
            if (publicKeyToken.IsEmpty)
                return "null";

            StringBuilder sb = new();
            foreach (byte b in publicKeyToken)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }

        private static CompatDifference CreateIdentityDifference(MetadataInformation left, MetadataInformation right, string format, string leftProperty, string rightProperty, string leftName, string rightName, AssemblyIdentity identity) =>
            new(left,
                right,
                DiagnosticIds.AssemblyIdentityMustMatch,
                string.Format(format, leftProperty, rightProperty, leftName, rightName),
                DifferenceType.Changed,
                identity.GetDisplayName());
    }
}
