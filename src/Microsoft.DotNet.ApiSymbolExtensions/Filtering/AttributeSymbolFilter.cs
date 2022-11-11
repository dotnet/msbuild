// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.ApiSymbolExtensions
{
    /// <summary>
    /// Implements the logic of filtering out attribute symbols. Reads the file with the list of attributes in DocId format.
    /// </summary>
    public class AttributeSymbolFilter : ISymbolFilter
    {
        private readonly HashSet<string> _attributesToExclude;

        public AttributeSymbolFilter(string[] attributeDocIdsFiles)
        {
            _attributesToExclude = new HashSet<string>(ReadDocIdsAttributes(attributeDocIdsFiles));
        }

        /// <summary>
        ///  Determines whether the attribute <see cref="ISymbol"/> should be included.
        /// </summary>
        /// <param name="symbol"><see cref="ISymbol"/> to evaluate.</param>
        /// <returns>True to include the <paramref name="symbol"/> or false to filter it out.</returns>
        public bool Include(ISymbol member)
        {
            if (member is INamedTypeSymbol namedType)
            {
                string? docId = namedType.GetDocumentationCommentId();
                if (docId != null && _attributesToExclude.Contains(docId))
                {
                    return false;
                }
            }
            return true;
        }

        private static IEnumerable<string> ReadDocIdsAttributes(IEnumerable<string> excludeAttributesFiles)
        {
            foreach (string filePath in excludeAttributesFiles)
            {
                foreach (string id in File.ReadAllLines(filePath))
                {
                    if (!string.IsNullOrWhiteSpace(id) && !id.StartsWith("#") && !id.StartsWith("//"))
                    {
                        yield return id.Trim();
                    }
                }
            }
        }
    }
}
