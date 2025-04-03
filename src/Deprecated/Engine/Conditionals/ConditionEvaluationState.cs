// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// THE ASSEMBLY BUILT FROM THIS SOURCE FILE HAS BEEN DEPRECATED FOR YEARS. IT IS BUILT ONLY TO PROVIDE
// BACKWARD COMPATIBILITY FOR API USERS WHO HAVE NOT YET MOVED TO UPDATED APIS. PLEASE DO NOT SEND PULL
// REQUESTS THAT CHANGE THIS FILE WITHOUT FIRST CHECKING WITH THE MAINTAINERS THAT THE FIX IS REQUIRED.

using System.Collections;
using System.Xml;

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
