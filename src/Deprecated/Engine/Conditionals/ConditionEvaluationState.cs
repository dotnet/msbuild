// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Xml;
using System.Text;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// All the state necessary for the evaluation of conditionals so that the expression tree 
    /// is stateless and reusable
    /// </summary>
    internal struct ConditionEvaluationState
    {
        internal XmlAttribute conditionAttribute;
        internal Expander expanderToUse;
        internal Hashtable conditionedPropertiesInProject;
        internal string parsedCondition;

        internal ConditionEvaluationState(XmlAttribute conditionAttribute, Expander expanderToUse, Hashtable conditionedPropertiesInProject, string parsedCondition)
        {
            this.conditionAttribute = conditionAttribute;
            this.expanderToUse = expanderToUse;
            this.conditionedPropertiesInProject = conditionedPropertiesInProject;
            this.parsedCondition = parsedCondition;
        }
    }
}
