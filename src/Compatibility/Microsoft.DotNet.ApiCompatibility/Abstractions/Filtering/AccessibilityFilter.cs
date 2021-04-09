using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.ApiCompatibility.Abstractions
{
    internal class AccessibilityFilter : IDiffingFilter
    {
        private readonly bool _includeInternalSymbols;

        internal AccessibilityFilter(bool includeInternalSymbols)
        {
            _includeInternalSymbols = includeInternalSymbols;
        }

        public bool Include(ISymbol symbol) =>
            symbol.DeclaredAccessibility == Accessibility.Public ||
            symbol.DeclaredAccessibility == Accessibility.Protected ||
            symbol.DeclaredAccessibility == Accessibility.ProtectedOrInternal ||
            (_includeInternalSymbols && symbol.DeclaredAccessibility != Accessibility.Private);
    }
}
