// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.CommandParsing
{
    internal class AliasAssignmentCoordinator
    {
        private IReadOnlyList<ITemplateParameter> _parameterDefinitions;
        private IDictionary<string, string> _longNameOverrides;
        private IDictionary<string, string> _shortNameOverrides;
        private HashSet<string> _takenAliases;
        private Dictionary<string, string> _longAssignments;
        private Dictionary<string, string> _shortAssignments;
        private HashSet<string> _invalidParams;
        private bool _calculatedAssignments;

        internal AliasAssignmentCoordinator(IReadOnlyList<ITemplateParameter> parameterDefinitions, IDictionary<string, string> longNameOverrides, IDictionary<string, string> shortNameOverrides, HashSet<string> takenAliases)
        {
            _parameterDefinitions = parameterDefinitions;
            _longNameOverrides = longNameOverrides;
            _shortNameOverrides = shortNameOverrides;
            _takenAliases = takenAliases;
            _longAssignments = new Dictionary<string, string>();
            _shortAssignments = new Dictionary<string, string>();
            _invalidParams = new HashSet<string>();
            _calculatedAssignments = false;
        }

        internal IReadOnlyDictionary<string, string> LongNameAssignments
        {
            get
            {
                EnsureAliasAssignments();
                return _longAssignments;
            }
        }

        internal IReadOnlyDictionary<string, string> ShortNameAssignments
        {
            get
            {
                EnsureAliasAssignments();
                return _shortAssignments;
            }
        }

        internal HashSet<string> InvalidParams
        {
            get
            {
                EnsureAliasAssignments();
                return _invalidParams;
            }
        }

        internal HashSet<string> TakenAliases
        {
            get
            {
                EnsureAliasAssignments();
                return _takenAliases;
            }
        }

        private void EnsureAliasAssignments()
        {
            if (_calculatedAssignments)
            {
                return;
            }

            Dictionary<string, KeyValuePair<string, string>> aliasAssignments = new Dictionary<string, KeyValuePair<string, string>>();
            Dictionary<string, ITemplateParameter> paramNamesNeedingAssignment = _parameterDefinitions.Where(x => x.Priority != TemplateParameterPriority.Implicit)
                                                                                    .ToDictionary(x => x.Name, x => x);

            SetupAssignmentsFromLongOverrides(paramNamesNeedingAssignment);
            SetupAssignmentsFromShortOverrides(paramNamesNeedingAssignment);
            SetupAssignmentsWithoutOverrides(paramNamesNeedingAssignment);

            _calculatedAssignments = true;
        }

        private void SetupAssignmentsFromLongOverrides(IReadOnlyDictionary<string, ITemplateParameter> paramNamesNeedingAssignment)
        {
            foreach (KeyValuePair<string, string> canonicalAndLong in _longNameOverrides.Where(x => paramNamesNeedingAssignment.ContainsKey(x.Key)))
            {
                string canonical = canonicalAndLong.Key;
                string longOverride = canonicalAndLong.Value;
                if (CommandAliasAssigner.TryAssignAliasesForParameter((x) => _takenAliases.Contains(x), canonical, longOverride, null, out IReadOnlyList<string> assignedAliases))
                {
                    // only deal with the long here, ignore the short for now
                    string longParam = assignedAliases.FirstOrDefault(x => x.StartsWith("--"));
                    if (!string.IsNullOrEmpty(longParam))
                    {
                        _longAssignments.Add(canonical, longParam);
                        _takenAliases.Add(longParam);
                    }
                }
                else
                {
                    _invalidParams.Add(canonical);
                }
            }
        }

        private void SetupAssignmentsFromShortOverrides(IReadOnlyDictionary<string, ITemplateParameter> paramNamesNeedingAssignment)
        {
            foreach (KeyValuePair<string, string> canonicalAndShort in _shortNameOverrides.Where(x => paramNamesNeedingAssignment.ContainsKey(x.Key)))
            {
                string canonical = canonicalAndShort.Key;
                string shortOverride = canonicalAndShort.Value;

                if (shortOverride == string.Empty)
                {
                    // it was explicitly empty string in the host file. If it wasn't specified, it'll be null
                    // this means there should be no short version
                    continue;
                }

                if (CommandAliasAssigner.TryAssignAliasesForParameter((x) => _takenAliases.Contains(x), canonical, null, shortOverride, out IReadOnlyList<string> assignedAliases))
                {
                    string shortParam = assignedAliases.FirstOrDefault(x => x.StartsWith("-") && !x.StartsWith("--"));
                    if (!string.IsNullOrEmpty(shortParam))
                    {
                        _shortAssignments.Add(canonical, shortParam);
                        _takenAliases.Add(shortParam);
                    }
                }
                else
                {
                    _invalidParams.Add(canonical);
                }
            }
        }

        private void SetupAssignmentsWithoutOverrides(IReadOnlyDictionary<string, ITemplateParameter> paramNamesNeedingAssignment)
        {
            foreach (ITemplateParameter parameterInfo in paramNamesNeedingAssignment.Values)
            {
                if (_longAssignments.ContainsKey(parameterInfo.Name) && _shortAssignments.ContainsKey(parameterInfo.Name))
                {
                    // already fully assigned
                    continue;
                }

                _longNameOverrides.TryGetValue(parameterInfo.Name, out string longOverride);
                _shortNameOverrides.TryGetValue(parameterInfo.Name, out string shortOverride);

                if (CommandAliasAssigner.TryAssignAliasesForParameter((x) => _takenAliases.Contains(x), parameterInfo.Name, longOverride, shortOverride, out IReadOnlyList<string> assignedAliases))
                {
                    if (shortOverride != string.Empty)
                    {
                        // explicit empty string in the host file means there should be no short name.
                        // but thats not the case here.
                        if (!_shortAssignments.ContainsKey(parameterInfo.Name))
                        {
                            // still needs a short version
                            string shortParam = assignedAliases.FirstOrDefault(x => x.StartsWith("-") && !x.StartsWith("--"));
                            if (!string.IsNullOrEmpty(shortParam))
                            {
                                _shortAssignments.Add(parameterInfo.Name, shortParam);
                                _takenAliases.Add(shortParam);
                            }
                        }
                    }

                    if (!_longAssignments.ContainsKey(parameterInfo.Name))
                    {
                        // still needs a long version
                        string longParam = assignedAliases.FirstOrDefault(x => x.StartsWith("--"));
                        if (!string.IsNullOrEmpty(longParam))
                        {
                            _longAssignments.Add(parameterInfo.Name, longParam);
                            _takenAliases.Add(longParam);
                        }
                    }
                }
                else
                {
                    _invalidParams.Add(parameterInfo.Name);
                }
            }
        }
    }
}
