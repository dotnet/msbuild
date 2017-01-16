// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Xml;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Collections;

namespace Microsoft.Build.UnitTests.QA
{
    /// <summary>
    /// Implements a QA test data cache to hold all the BuildRequestDefinitions.
    /// </summary>
    internal class TestDataProvider : ITestDataProvider, IBuildComponent, IDisposable
    {
        #region Data members

        /// <summary>
        /// The BuildRequestDefinition
        /// </summary>
        private Dictionary<int, RequestDefinition> _definitions;

        /// <summary>
        /// Configuration cache component
        /// </summary>
        private IConfigCache _configurationCache;

        /// <summary>
        /// The results cache component being used by the host
        /// </summary>
        private IResultsCache _resultsCache;

        /// <summary>
        /// Key assigned to a particular configuration. By default key starts at 2. 1 is reserved for the root
        /// </summary>
        private int _key;

        /// <summary>
        /// Queue that holds the new build requests from the engine
        /// </summary>
        private Queue<BuildRequest> _newRequests;

        /// <summary>
        /// Queue that holds the new configuration requests from the engine
        /// </summary>
        private Queue<BuildRequestConfiguration> _newConfigurations;

        /// <summary>
        /// Queue that holds the results from the engine
        /// </summary>
        private Queue<ResultFromEngine> _newResults;

        /// <summary>
        /// Exception thrown by the engine
        /// </summary>
        private Exception _engineException;

        /// <summary>
        /// Thread which is responsible for processing the requests and results
        /// </summary>
        private Thread _processorThread;

        /// <summary>
        /// Event that signals the processor thread to do something
        /// </summary>
        private AutoResetEvent _processorThreadResume;

        /// <summary>
        /// Event that signals the processor thread to exit
        /// </summary>
        private AutoResetEvent _processorThreadExit;

        /// <summary>
        /// Indicates if the processor thread has already exited
        /// </summary>
        private bool _processorThreadExited;

        #endregion

        #region Public methods

        /// <summary>
        /// Creates a new BuildRequestDefinition cache.
        /// </summary>
        public TestDataProvider()
        {
            _definitions = new Dictionary<int, RequestDefinition>();
            _configurationCache = null;
            _key = 2;
            _newConfigurations = new Queue<BuildRequestConfiguration>();
            _newRequests = new Queue<BuildRequest>();
            _newResults = new Queue<ResultFromEngine>();
            _engineException = null;
            _processorThreadResume = new AutoResetEvent(false);
            _processorThreadExit = new AutoResetEvent(false);
            _processorThreadExited = false;
            _processorThread = new Thread(ProcessorThreadProc);
            _processorThread.Name = "Test Data provider processor thread";
            _processorThread.Start();
        }

        /// <summary>
        /// Adds a new definition to the cache. Returns the key associated with this definition so that it can be
        /// used as the configuration id also.
        /// </summary>
        public int AddDefinition(RequestDefinition definition)
        {
            int newKey;

            if (_configurationCache.GetMatchingConfiguration(definition.UnresolvedConfiguration) != null)
            {
                throw new InvalidOperationException("Multiple request definition with the same configuration cannot be added");
            }
            else
            {
                lock (_definitions)
                {
                    newKey = _key++;
                    _definitions.Add(newKey, definition);
                }
            }

            return newKey;
        }

        /// <summary>
        /// Adds a new configuration to the configuration cache if one is not already there.
        /// Some times this method can be called twice specially when 2 request contains the same project
        /// where one is a request from the engine (building a reference) and 1 is a request from the test. 
        /// If the configuration already exists in the cache then we will just use that.
        /// </summary>
        public BuildRequestConfiguration CreateConfiguration(RequestDefinition definition)
        {
            BuildRequestConfiguration unresolvedConfig = definition.UnresolvedConfiguration;
            BuildRequestConfiguration newConfig = _configurationCache.GetMatchingConfiguration(unresolvedConfig);
            if (newConfig == null)
            {
                int newId = GetIdForUnresolvedConfiguration(unresolvedConfig);
                newConfig = new BuildRequestConfiguration(newId, new BuildRequestData(definition.FileName, definition.GlobalProperties.ToDictionary(), definition.ToolsVersion, new string[0], null), "2.0");
                _configurationCache.AddConfiguration(newConfig);
                newConfig.Project = definition.ProjectDefinition.GetMSBuildProjectInstance();
            }

            return newConfig;
        }

        /// <summary>
        /// Dictionary of request definitions where the key is the configuration id and the value is the request definition for that configuration
        /// </summary>
        public Dictionary<int, RequestDefinition> RequestDefinitions
        {
            get
            {
                return _definitions;
            }
        }

        #endregion

        #region Public indexers

        /// <summary>
        /// Returns the BuildRequestDefinition cached under the specified definition id.
        /// </summary>
        public RequestDefinition this[int definitionId]
        {
            get
            {
                lock (_definitions)
                {
                    return _definitions[definitionId];
                }
            }
        }

        #endregion

        #region ITestDataProvider Members

        /// <summary>
        /// Adds a new Request to the Queue and signals the ProcessorThread to do something
        /// </summary>
        public BuildRequest NewRequest
        {
            set
            {
                if (_processorThreadExited)
                {
                    return;
                }

                lock (_newRequests)
                {
                    _newRequests.Enqueue(value);
                }

                _processorThreadResume.Set();
            }
        }

        /// <summary>
        /// Adds a new Configuration to the Queue and signals the ProcessorThread to do something
        /// </summary>
        public BuildRequestConfiguration NewConfiguration
        {
            set
            {
                if (_processorThreadExited)
                {
                    return;
                }

                lock (_newConfigurations)
                {
                    _newConfigurations.Enqueue(value);
                }

                _processorThreadResume.Set();
            }
        }

        /// <summary>
        /// Adds a new result to the Queue and signals the ProcessorThread to do something
        /// </summary>
        public ResultFromEngine NewResult
        {
            set
            {
                if (_processorThreadExited)
                {
                    return;
                }

                lock (_newResults)
                {
                    _newResults.Enqueue(value);
                }

                _processorThreadResume.Set();
            }
        }

        /// <summary>
        /// Exception raised by the engine. This is forwarded to all the definitions.
        /// Signals the ProcessorThread to do something
        /// </summary>
        public Exception EngineException
        {
            set
            {
                if (_processorThreadExited)
                {
                    return;
                }

                _engineException = value;
                _processorThreadResume.Set();
            }
        }

        #endregion

        #region Private methods

        /// <summary>
        /// Given a definition find and return the key value associated with it which will also act as the config id.
        /// Sometimes we may have multiple definitions for the same project file name. So we want to make sure that we pick the correct one.
        /// </summary>
        private int GetIdForUnresolvedConfiguration(BuildRequestConfiguration config)
        {
            int id = -1;

            lock (_definitions)
            {
                foreach (KeyValuePair<int, RequestDefinition> pair in _definitions)
                {
                    if (pair.Value.AreSameDefinitions(config))
                    {
                        id = pair.Key;
                        break;
                    }
                }
            }

            return id;
        }

        #endregion

        #region Processor Thread

        /// <summary>
        /// Main thread process which is responsible for processing the configuration
        /// and build requests. Currently we process all items fromeach of the Queue.
        /// If there is an exception then send it to the definitions. Let the remining 
        /// Queue be processed then exit the thread
        /// </summary>
        private void ProcessorThreadProc()
        {
            WaitHandle[] waitHandles = { _processorThreadExit, _processorThreadResume };
            while (!_processorThreadExited)
            {
                int handle = WaitHandle.WaitAny(waitHandles);
                switch (handle)
                {
                    case 0:
                        // exit
                        _processorThreadExited = true;
                        break;

                    case 1:
                        // something to process
                        if (_engineException != null)
                        {
                            foreach (RequestDefinition definition in _definitions.Values)
                            {
                                definition.RaiseEngineException(_engineException);
                            }

                            _processorThreadExited = true;
                        }

                        // Process new configuration requests
                        if (_newConfigurations != null && _newConfigurations.Count > 0)
                        {
                            BuildRequestConfiguration config = null;
                            config = _newConfigurations.Peek();
                            while (config != null)
                            {
                                int newConfigId = this.GetIdForUnresolvedConfiguration(config);
                                RequestDefinition definition = this[newConfigId];
                                definition.RaiseOnNewConfigurationRequest(config);

                                lock (_newConfigurations)
                                {
                                    _newConfigurations.Dequeue();
                                }

                                if (_newConfigurations.Count > 0)
                                {
                                    config = _newConfigurations.Peek();
                                }
                                else
                                {
                                    config = null;
                                }
                            }
                        }

                        // Process new build requests
                        if (_newRequests != null && _newRequests.Count > 0)
                        {
                            BuildRequest request = null;
                            request = _newRequests.Peek();
                            while (request != null)
                            {
                                RequestDefinition definition = this[request.ConfigurationId];
                                definition.RaiseOnNewBuildRequest(request);

                                lock (_newRequests)
                                {
                                    _newRequests.Dequeue();
                                }

                                if (_newRequests.Count > 0)
                                {
                                    request = _newRequests.Peek();
                                }
                                else
                                {
                                    request = null;
                                }
                            }
                        }

                        // Process results for completed requests
                        if (_newResults != null && _newResults.Count > 0)
                        {
                            ResultFromEngine result = null;
                            result = _newResults.Peek();
                            while (result != null)
                            {
                                RequestDefinition definition = this[result.Request.ConfigurationId];
                                definition.RaiseOnBuildRequestCompleted(result.Request, result.Result);

                                lock (_newResults)
                                {
                                    _newResults.Dequeue();
                                }

                                if (_newResults.Count > 0)
                                {
                                    result = _newResults.Peek();
                                }
                                else
                                {
                                    result = null;
                                }
                            }
                        }

                        break;

                    default:
                        // Unknown event
                        _processorThreadExited = true;
                        throw new InvalidOperationException("Unknown wait signal received by the ProcessorThread");
                }
            }
        }

        #endregion

        #region IBuildComponent Members

        /// <summary>
        /// Save the configuration cache information from the host
        /// </summary>
        public void InitializeComponent(IBuildComponentHost host)
        {
            _configurationCache = (IConfigCache)host.GetComponent(BuildComponentType.ConfigCache);
            _resultsCache = (IResultsCache)host.GetComponent(BuildComponentType.ResultsCache);
        }

        /// <summary>
        /// Cleanup
        /// </summary>
        public void ShutdownComponent()
        {
            // If the processor thread is still there then signal it to go away
            // Wait for QAMockHost.globalTimeOut seconds for the thread to go away or complete. If not then abort it.
            if (!_processorThreadExited)
            {
                _processorThreadExit.Set();
                if (!_processorThread.Join(QAMockHost.globalTimeOut))
                {
                    _processorThread.Abort();
                }

                _processorThread = null;
            }

            // dispose all the definition object here.
            foreach (RequestDefinition definition in _definitions.Values)
            {
                definition.Dispose();
            }

            _definitions.Clear();
            _newResults.Clear();
            _newRequests.Clear();
            _newConfigurations.Clear();
            _newRequests = null;
            _newResults = null;
            _newConfigurations = null;
            _configurationCache = null;
            _resultsCache = null;
        }

        #endregion

        #region Public properties

        /// <summary>
        /// Result cache build component being used by the host
        /// </summary>
        public IResultsCache ResultsCache
        {
            get
            {
                return _resultsCache;
            }
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            _processorThreadResume.Close();
            _processorThreadExit.Close();
            GC.SuppressFinalize(this);
        }

        #endregion
    }

    /// <summary>
    /// Holds the result and request pair returned by the engine
    /// </summary>
    internal class ResultFromEngine
    {
        /// <summary>
        /// Associated request for the result
        /// </summary>
        private BuildRequest _request;
        /// <summary>
        /// Build result
        /// </summary>
        private BuildResult _result;

        /// <summary>
        /// Constructor. This is the only way of setting the data members.
        /// </summary>
        public ResultFromEngine(BuildRequest request, BuildResult result)
        {
            _request = request;
            _result = result;
        }

        /// <summary>
        /// Request associated with the result
        /// </summary>
        public BuildRequest Request
        {
            get
            {
                return _request;
            }
        }

        /// <summary>
        /// Build result
        /// </summary>
        public BuildResult Result
        {
            get
            {
                return _result;
            }
        }
    }
}