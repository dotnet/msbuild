// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.ApiCompatibility.Abstractions;
using System.Collections.Generic;

namespace Microsoft.DotNet.ApiCompatibility
{
    public class DiagnosticBag<T> where T : IDiagnostic
    {
        private readonly Dictionary<string, HashSet<string>> _ignore;
        private readonly HashSet<string> _noWarn;

        private readonly List<T> _differences = new();

        public DiagnosticBag(string noWarn, (string diagnosticId, string referenceId)[] ignoredDifferences)
        {
            _noWarn = new HashSet<string>(noWarn?.Split(';'));
            _ignore = new Dictionary<string, HashSet<string>>();

            foreach (var ignored in ignoredDifferences)
            {
                if (!_ignore.TryGetValue(ignored.diagnosticId, out HashSet<string> members))
                {
                    members = new HashSet<string>();
                    _ignore.Add(ignored.diagnosticId, members);
                }

                members.Add(ignored.referenceId);
            }
        }

        public void AddRange(IEnumerable<T> differences)
        {
            foreach (T difference in differences)
                Add(difference);
        }

        public void Add(T difference)
        {
            if (_noWarn.Contains(difference.DiagnosticId))
                return;

            if (_ignore.TryGetValue(difference.DiagnosticId, out HashSet<string> members))
            {
                if (members.Contains(difference.ReferenceId))
                {
                    return;
                }
            }

            _differences.Add(difference);
        }

        public IEnumerable<T> Differences => _differences;
    }
}
