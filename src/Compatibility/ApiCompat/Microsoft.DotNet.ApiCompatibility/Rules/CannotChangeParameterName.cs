// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    /// <summary>
    /// This class implements a rule to check that the parameter names between public methods do not change.
    /// </summary>
    public class CannotChangeParameterName : IRule
    {
        public CannotChangeParameterName(IRuleSettings settings, IRuleRegistrationContext context) =>
            context.RegisterOnMemberSymbolAction(RunOnMemberSymbol);

        private void RunOnMemberSymbol(
            ISymbol? left,
            ISymbol? right,
            ITypeSymbol leftContainingType,
            ITypeSymbol rightContainingType,
            MetadataInformation leftMetadata,
            MetadataInformation rightMetadata,
            IList<CompatDifference> differences)
        {
            if (left is not IMethodSymbol leftMethod || right is not IMethodSymbol rightMethod)
            {
                return;
            }

            Debug.Assert(leftMethod.Parameters.Length == rightMethod.Parameters.Length);

            for (int i = 0; i < leftMethod.Parameters.Length; i++)
            {
                IParameterSymbol leftParam = leftMethod.Parameters[i];
                IParameterSymbol rightParam = rightMethod.Parameters[i];

                if (!leftParam.Name.Equals(rightParam.Name))
                {
                    differences.Add(new CompatDifference(
                        leftMetadata,
                        rightMetadata,
                        DiagnosticIds.CannotChangeParameterName,
                        string.Format(Resources.CannotChangeParameterName, left, leftParam.Name, rightParam.Name),
                        DifferenceType.Changed,
                        $"{leftMethod.GetDocumentationCommentId()}${i}"));
                }
            }
        }
    }
}
