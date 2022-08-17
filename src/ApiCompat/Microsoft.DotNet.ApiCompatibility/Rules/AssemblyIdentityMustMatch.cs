// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    public class AssemblyIdentityMustMatch : IRule
    {
        private readonly RuleSettings _settings;

        public AssemblyIdentityMustMatch(RuleSettings settings, IRuleRegistrationContext context)
        {
            _settings = settings;
            context.RegisterOnAssemblySymbolAction(RunOnAssemblySymbol);
        }

        private void RunOnAssemblySymbol(IAssemblySymbol? left, IAssemblySymbol? right, MetadataInformation leftMetadata, MetadataInformation rightMetadata, IList<CompatDifference> differences)
        {
            if (left == null && right != null)
            {
                differences.Add(new CompatDifference(
                    leftMetadata,
                    rightMetadata,
                    DiagnosticIds.MatchingAssemblyDoesNotExist,
                    string.Format(Resources.AssemblyNameDoesNotExist, leftMetadata, right.Identity.Name),
                    DifferenceType.Removed,
                    right.Identity.GetDisplayName()));
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
                    Resources.AssembyCultureDoesNotMatch,
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
                    Resources.AssembyVersionIsNotCompatible,
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
                    Resources.AssembyVersionDoesNotMatch,
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
