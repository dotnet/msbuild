// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Cli
{
    internal class TemplateGroupParameterSet : IParameterSet
    {
        private readonly IReadOnlyList<IParameterSet> _parameterSetList;

        private IEnumerable<ITemplateParameter> _parameterDefinitions;

        private IDictionary<ITemplateParameter, object> _resolvedValues;

        internal TemplateGroupParameterSet(IReadOnlyList<IParameterSet> parameterSetList)
        {
            _parameterSetList = parameterSetList;
        }

        public IEnumerable<ITemplateParameter> ParameterDefinitions
        {
            get
            {
                if (_parameterDefinitions == null)
                {
                    IDictionary<string, ITemplateParameter> combinedParams = new Dictionary<string, ITemplateParameter>();
                    IDictionary<string, Dictionary<string, ParameterChoice>> combinedChoices = new Dictionary<string, Dictionary<string, ParameterChoice>>();

                    // gather info
                    foreach (IParameterSet paramSet in _parameterSetList)
                    {
                        foreach (ITemplateParameter parameter in paramSet.ParameterDefinitions)
                        {
                            // add the parameter to the combined list
                            if (!combinedParams.ContainsKey(parameter.Name))
                            {
                                combinedParams.Add(parameter.Name, parameter);
                            }

                            // build the combined choice lists
                            if (parameter.Choices != null)
                            {
                                Dictionary<string, ParameterChoice> combinedChoicesForParam;
                                if (!combinedChoices.TryGetValue(parameter.Name, out combinedChoicesForParam))
                                {
                                    combinedChoicesForParam = new Dictionary<string, ParameterChoice>();
                                    combinedChoices.Add(parameter.Name, combinedChoicesForParam);
                                }

                                foreach (KeyValuePair<string, ParameterChoice> choiceAndDescription in parameter.Choices)
                                {
                                    if (!combinedChoicesForParam.ContainsKey(choiceAndDescription.Key))
                                    {
                                        combinedChoicesForParam[choiceAndDescription.Key] = choiceAndDescription.Value;
                                    }
                                }
                            }
                        }
                    }

                    // create the combined params
                    IList<ITemplateParameter> outputParams = new List<ITemplateParameter>();
                    foreach (KeyValuePair<string, ITemplateParameter> paramInfo in combinedParams)
                    {
                        if (!string.Equals(paramInfo.Value.DataType, "choice", StringComparison.OrdinalIgnoreCase))
                        {
                            outputParams.Add(paramInfo.Value);
                        }
                        else
                        {
                            Dictionary<string, ParameterChoice> choicesAndDescriptions;
                            if (!combinedChoices.TryGetValue(paramInfo.Key, out choicesAndDescriptions))
                            {
                                choicesAndDescriptions = new Dictionary<string, ParameterChoice>();
                            }

                            ITemplateParameter combinedParameter = new TemplateParameter
                            {
                                Documentation = paramInfo.Value.Documentation,
                                Name = paramInfo.Value.Name,
                                Priority = paramInfo.Value.Priority,
                                Type = paramInfo.Value.Type,
                                IsName = paramInfo.Value.IsName,
                                DefaultValue = paramInfo.Value.DefaultValue,
                                DataType = paramInfo.Value.DataType,
                                Choices = choicesAndDescriptions
                            };

                            if (combinedParameter is IAllowDefaultIfOptionWithoutValue combinedParamWithDefault
                                && paramInfo.Value is IAllowDefaultIfOptionWithoutValue paramInfoValueWithDefault)
                            {
                                combinedParamWithDefault.DefaultIfOptionWithoutValue = paramInfoValueWithDefault.DefaultIfOptionWithoutValue;
                                outputParams.Add(combinedParamWithDefault as TemplateParameter);
                            }
                            else
                            {
                                outputParams.Add(combinedParameter);
                            }
                        }
                    }

                    _parameterDefinitions = outputParams;
                }

                return _parameterDefinitions;
            }
        }

        public IEnumerable<string> RequiredBrokerCapabilities => Enumerable.Empty<string>();

        public IDictionary<ITemplateParameter, object> ResolvedValues
        {
            get
            {
                if (_resolvedValues == null)
                {
                    IDictionary<ITemplateParameter, object> resolvedValues = new Dictionary<ITemplateParameter, object>();

                    foreach (ITemplateParameter groupParameter in ParameterDefinitions)
                    {
                        // take the first value from the first group that has a a value for this parameter.
                        foreach (IParameterSet baseParamSet in _parameterSetList)
                        {
                            ITemplateParameter baseParam = baseParamSet.ParameterDefinitions.FirstOrDefault(x => string.Equals(x.Name, groupParameter.Name, StringComparison.OrdinalIgnoreCase));
                            if (baseParam != null)
                            {
                                if (baseParamSet.ResolvedValues.TryGetValue(baseParam, out object value))
                                {
                                    resolvedValues.Add(groupParameter, value);
                                    break;  // from the inner loop
                                }
                            }
                        }
                    }

                    _resolvedValues = resolvedValues;
                }

                return _resolvedValues;
            }
        }

        public bool TryGetParameterDefinition(string name, out ITemplateParameter parameter)
        {
            parameter = ParameterDefinitions.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));

            if (parameter != null)
            {
                return true;
            }

            parameter = new TemplateParameter
            {
                Documentation = string.Empty,
                Name = name,
                Priority = TemplateParameterPriority.Optional,
                Type = "string",
                IsName = false,
                DefaultValue = string.Empty,
                DataType = "string",
                Choices = null
            };

            if (parameter is IAllowDefaultIfOptionWithoutValue parameterWithNoValueDefault)
            {
                parameterWithNoValueDefault.DefaultIfOptionWithoutValue = null;
                parameter = parameterWithNoValueDefault as TemplateParameter;
            }

            return true;
        }
    }
}
