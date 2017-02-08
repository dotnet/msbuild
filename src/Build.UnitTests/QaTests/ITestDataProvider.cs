// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Xml;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Collections;

namespace Microsoft.Build.UnitTests.QA
{
    /// <summary>
    /// An interfacing representing a build request definition cache.
    /// </summary>
    internal interface ITestDataProvider
    {
        /// <summary>
        /// Indexer to get the Nth request definition from the provider
        /// </summary>
        RequestDefinition this[int index] { get; }

        /// <summary>
        /// Adds a new definition to the cache. Returns the key associated with this definition so that it can be
        /// used as the configuration id also.
        /// </summary>
        int AddDefinition(RequestDefinition definition);

        /// <summary>
        /// Adds a new configuration to the configuration cache if one is not already there. Also adds to the configuration cache.
        /// </summary>
        BuildRequestConfiguration CreateConfiguration(RequestDefinition definition);

        /// <summary>
        /// Adds a new Request to the Enqueue(value);
        /// </summary>
        BuildRequest NewRequest { set; }

        /// <summary>
        /// Adds a new Configuration to the Enqueue(value);
        /// </summary>
        BuildRequestConfiguration NewConfiguration { set; }

        /// <summary>
        /// Adds a new result to the Queue
        /// </summary>
        ResultFromEngine NewResult { set; }

        /// <summary>
        /// Exception raised by the engine. This is forwarded to all the definitions
        /// </summary>
        Exception EngineException { set; }

        /// <summary>
        /// Dictionary of request definitions where the key is the configuration id and the value is the request definition for that configuration
        /// </summary>
        Dictionary<int, RequestDefinition> RequestDefinitions { get; }
    }
}
