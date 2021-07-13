// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    public class AssemblyIdentityMustMatch : Rule
    {
        public override void Initialize(RuleRunnerContext context)
        {
            context.RegisterOnAssemblySymbolAction(RunOnAssemblySymbol);
        }

        private void RunOnAssemblySymbol(IAssemblySymbol left, IAssemblySymbol right, string leftName, string rightName, IList<CompatDifference> differences)
        {
            if (left == null && right != null)
            {
                differences.Add(new CompatDifference(DiagnosticIds.MatchingAssemblyDoesNotExist, string.Format(Resources.AssemblyNameDoesNotExist, leftName, right.Identity.Name), DifferenceType.Removed, right.Identity.GetDisplayName()));
                return;
            }

            if (left != null && right == null)
            {
                differences.Add(new CompatDifference(DiagnosticIds.MatchingAssemblyDoesNotExist, string.Format(Resources.AssemblyNameDoesNotExist, rightName, left.Identity.Name), DifferenceType.Added, left.Identity.GetDisplayName()));
                return;
            }

            AssemblyIdentity leftIdentity = left.Identity;
            AssemblyIdentity rightIdentity = right.Identity;

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
                differences.Add(CreateIdentityDifference(Resources.AssemblyNameDoesNotMatch, leftAssemblyName, rightAssemblyName, leftName, rightName, rightIdentity));
            }

            if (leftAssemblyCulture != rightAssemblyCulture)
            {
                differences.Add(CreateIdentityDifference(Resources.AssembyCultureDoesNotMatch, leftAssemblyCulture, rightAssemblyCulture, leftName, rightName, rightIdentity));
            }
 
            if (rightAssemblyVersion < leftAssemblyVersion)
            {
                differences.Add(CreateIdentityDifference(Resources.AssembyVersionIsNotCompatible, rightAssemblyVersion.ToString(), leftAssemblyVersion.ToString(), rightName, leftName, rightIdentity));
            }
            else if (Settings.StrictMode && leftAssemblyVersion < rightAssemblyVersion)
            {
                differences.Add(CreateIdentityDifference(Resources.AssembyVersionDoesNotMatch, leftAssemblyVersion.ToString(), rightAssemblyVersion.ToString(), leftName, rightName, leftIdentity));
            }

            bool isLeftRetargetable = (left.Identity.Flags & AssemblyNameFlags.Retargetable) != 0;
            bool isRightRetargetable = (right.Identity.Flags & AssemblyNameFlags.Retargetable) != 0;
            if (!leftAssemblyPublicKeyToken.IsEmpty && !isLeftRetargetable && !leftAssemblyPublicKeyToken.SequenceEqual(rightAssemblyPublicKeyToken))
            {
                differences.Add(CreateIdentityDifference(
                    Resources.AssemblyPublicKeyTokenDoesNotMatch,
                    GetStringRepresentation(leftAssemblyPublicKeyToken), 
                    GetStringRepresentation(rightAssemblyPublicKeyToken), 
                    leftName, 
                    rightName,
                    rightIdentity));
            }
            else if (Settings.StrictMode && !rightAssemblyPublicKeyToken.IsEmpty && !isRightRetargetable && !rightAssemblyPublicKeyToken.SequenceEqual(leftAssemblyPublicKeyToken))
            {
                differences.Add(CreateIdentityDifference(
                    Resources.AssemblyPublicKeyTokenDoesNotMatch,
                    GetStringRepresentation(rightAssemblyPublicKeyToken), 
                    GetStringRepresentation(leftAssemblyPublicKeyToken), 
                    rightName,
                    leftName,
                    leftIdentity));
            }
        }

        private static string GetStringRepresentation(ReadOnlySpan<byte> publicKeyToken)
        {
            if (publicKeyToken.IsEmpty)
                return "null";

            StringBuilder sb = new StringBuilder();
            foreach (var b in publicKeyToken)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }

        private CompatDifference CreateIdentityDifference(string format, string leftProperty, string rightProperty, string leftName, string rightName, AssemblyIdentity identity) =>
            new CompatDifference(DiagnosticIds.AssemblyIdentityMustMatch, string.Format(format, leftProperty, rightProperty, leftName, rightName), DifferenceType.Changed, identity.GetDisplayName());
    }
}
