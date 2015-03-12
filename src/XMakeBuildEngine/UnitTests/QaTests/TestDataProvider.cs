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
        private Dictionary<int, RequestDefinition> definitions;

        /// <summary>
        /// Configuration cache component
        /// </summary>
        private IConfigCache configurationCache;

        /// <summary>
        /// The results cache component being used by the host
        /// </summary>
        private IResultsCache resultsCache;

        /// <summary>
        /// Key assigned to a particular configuration. By default key starts at 2. 1 is reserved for the root
        /// </summary>
        private int key;

        /// <summary>
        /// Queue that holds the new build requests from the engine
        /// </summary>
        private Queue<BuildRequest> newRequests;

        /// <summary>
        /// Queue that holds the new configuration requests from the engine
        /// </summary>
        private Queue<BuildRequestConfiguration> newConfigurations;

        /// <summary>
        /// Queue that holds the results from the engine
        /// </summary>
        private Queue<ResultFromEngine> newResults;

        /// <summary>
        /// Exception thrown by the engine
        /// </summary>
        private Exception engineException;

        /// <summary>
        /// Thread which is responsible for processing the requests and results
        /// </summary>
        private Thread processorThread;

        /// <summary>
        /// Event that signals the processor thread to do something
        /// </summary>
        private AutoResetEvent processorThreadResume;

        /// <summary>
        /// Event that signals the processor thread to exit
        /// </summary>
        private AutoResetEvent processorThreadExit;

        /// <summary>
        /// Indicates if the processor thread has already exited
        /// </summary>
        private bool processorThreadExited;

        #endregion

        #region Public methods

        /// <summary>
        /// Creates a new BuildRequestDefinition cache.
        /// </summary>
        public TestDataProvider()
        {
            this.definitions = new Dictionary<int, RequestDefinition>();
            this.configurationCache = null;
            this.key = 2;
            this.newConfigurations = new Queue<BuildRequestConfiguration>();
            this.newRequests = new Queue<BuildRequest>();
            this.newResults = new Queue<ResultFromEngine>();
            this.engineException = null;
            this.processorThreadResume = new AutoResetEvent(false);
            this.processorThreadExit = new AutoResetEvent(false);
            this.processorThreadExited = false;
            this.processorThread = new Thread(ProcessorThreadProc);
            this.processorThread.Name = "Test Data provider processor thread";
            this.processorThread.Start();
        }

        /// <summary>
        /// Adds a new definition to the cache. Returns the key associated with this definition so that it can be
        /// used as the configuration id also.
        /// </summary>
        public int AddDefinition(RequestDefinition definition)
        {
            int newKey;

            if (this.configurationCache.GetMatchingConfiguration(definition.UnresolvedConfiguration) != null)
            {
                throw new InvalidOperationException("Multiple request definition with the same configuration cannot be added");
            }
            else
            {
                lock (this.definitions)
                {
                    newKey = key++;
                    this.definitions.Add(newKey, definition);
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
            BuildRequestConfiguration newConfig = this.configurationCache.GetMatchingConfiguration(unresolvedConfig);
            if (newConfig == null)
            {
                int newId = GetIdForUnresolvedConfiguration(unresolvedConfig);
                newConfig = new BuildRequestConfiguration(newId, new BuildRequestData(definition.FileName, definition.GlobalProperties.ToDictionary(), definition.ToolsVersion, new string[0], null), "2.0");
                this.configurationCache.AddConfiguration(newConfig);
                newConfig.Project = definition.ProjectDefinition.GetMSBuildProjectInstance();
            }

            return newConfig;
        }

        /// <summary>
        /// Dictonary of request definitions where the key is the configuration id and the value is the request defination for that configuration
        /// </summary>
        public Dictionary<int, RequestDefinition> RequestDefinitions
        {
            get
            {
                return this.definitions;
            }
        }

        #endregion

        #region Public indexers

        /// <summary>
        /// Returns the BuildRequestDefinition cached under the specified definition id.
        /// </summary>
        public RequestDefinition this[int definationId]
        {
            get
            {
                lock (this.definitions)
                {
                    return this.definitions[definationId];
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
                if (this.processorThreadExited)
                {
                    return;
                }

                lock (this.newRequests)
                {
                    this.newRequests.Enqueue(value);
                }

                this.processorThreadResume.Set();
            }
        }

        /// <summary>
        /// Adds a new Configuration to the Queue and signals the ProcessorThread to do something
        /// </summary>
        public BuildRequestConfiguration NewConfiguration
        {
            set
            {
                if (this.processorThreadExited)
                {
                    return;
                }

                lock(this.newConfigurations)
                {
                    this.newConfigurations.Enqueue(value);
                }

                this.processorThreadResume.Set();
            }
        }

        /// <summary>
        /// Adds a new result to the Queue and signals the ProcessorThread to do something
        /// </summary>
        public ResultFromEngine NewResult
        {
            set
            {
                if (this.processorThreadExited)
                {
                    return;
                }

                lock (this.newResults)
                {
                    this.newResults.Enqueue(value);
                }

                this.processorThreadResume.Set();
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
                if (this.processorThreadExited)
                {
                    return;
                }

                this.engineException = value;
                this.processorThreadResume.Set();
            }
        }

        #endregion

        #region Private methods

        /// <summary>
        /// Given adefination find and return the key value associated with it which will also act as the config id.
        /// Sometimes we may have multiple definitions for the same project file name. So we want to make sure that we pick the correct one.
        /// </summary>
        private int GetIdForUnresolvedConfiguration(BuildRequestConfiguration config)
        {
            int id = -1;

            lock (this.definitions)
            {
                foreach (KeyValuePair<int, RequestDefinition> pair in this.definitions)
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
            WaitHandle[] waitHandles = { this.processorThreadExit, this.processorThreadResume };
            while (!this.processorThreadExited)
            {
                int handle = WaitHandle.WaitAny(waitHandles);
                switch (handle)
                {
                    case 0: 
                        // exit
                        this.processorThreadExited = true;
                        break;

                    case 1:
                        // something to process
                        if (this.engineException != null)
                        {
                            foreach (RequestDefinition definition in this.definitions.Values)
                            {
                                definition.RaiseEngineException(this.engineException);
                            }

                            this.processorThreadExited = true;
                        }

                        // Process new configuration requests
                        if (this.newConfigurations != null && this.newConfigurations.Count > 0)
                        {
                            BuildRequestConfiguration config = null;
                            config = this.newConfigurations.Peek();
                            while (config != null)
                            {
                                int newConfigId = this.GetIdForUnresolvedConfiguration(config);
                                RequestDefinition definition = this[newConfigId];
                                definition.RaiseOnNewConfigurationRequest(config);

                                lock (this.newConfigurations)
                                {
                                    this.newConfigurations.Dequeue();
                                }

                                if (this.newConfigurations.Count > 0)
                                {
                                    config = this.newConfigurations.Peek();
                                }
                                else
                                {
                                    config = null;
                                }
                            }
                        }
 
                        // Process new build requests
                        if (this.newRequests != null && this.newRequests.Count > 0)
                        {
                            BuildRequest request = null;
                            request = this.newRequests.Peek();
                            while (request != null)
                            {
                                RequestDefinition definition = this[request.ConfigurationId];
                                definition.RaiseOnNewBuildRequest(request);

                                lock (this.newRequests)
                                {
                                    this.newRequests.Dequeue();
                                }

                                if (this.newRequests.Count > 0)
                                {
                                    request = this.newRequests.Peek();
                                }
                                else
                                {
                                    request = null;
                                }
                            }
                        }
 
                        // Process results for completed requests
                        if (this.newResults != null && this.newResults.Count > 0)
                        {
                            ResultFromEngine result = null;
                            result = this.newResults.Peek();
                            while (result != null)
                            {
                                RequestDefinition definition = this[result.Request.ConfigurationId];
                                definition.RaiseOnBuildRequestCompleted(result.Request, result.Result);

                                lock (this.newResults)
                                {
                                    this.newResults.Dequeue();
                                }

                                if (this.newResults.Count > 0)
                                {
                                    result = this.newResults.Peek();
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
                        this.processorThreadExited = true;
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
            this.configurationCache = (IConfigCache)host.GetComponent(BuildComponentType.ConfigCache);
            this.resultsCache = (IResultsCache)host.GetComponent(BuildComponentType.ResultsCache);
        }

        /// <summary>
        /// Cleanup
        /// </summary>
        public void ShutdownComponent()
        {
            // If the processor thread is still there then signal it to go away
            // Wait for QAMockHost.globalTimeOut seconds for the thread to go away or complete. If not then abort it.
            if (!this.processorThreadExited)
            {
                this.processorThreadExit.Set();
                if(!this.processorThread.Join(QAMockHost.globalTimeOut))
                {
                    this.processorThread.Abort();
                }

                this.processorThread = null;
            }

            // dispose all the definition object here.
            foreach (RequestDefinition definition in this.definitions.Values)
            {
                definition.Dispose();
            }

            this.definitions.Clear();
            this.newResults.Clear();
            this.newRequests.Clear();
            this.newConfigurations.Clear();
            this.newRequests = null;
            this.newResults = null;
            this.newConfigurations = null;
            this.configurationCache = null;
            this.resultsCache = null;
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
                return this.resultsCache;
            }
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            this.processorThreadResume.Close();
            this.processorThreadExit.Close();
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
        private BuildRequest request;
        /// <summary>
        /// Build result
        /// </summary>
        private BuildResult result;
        
        /// <summary>
        /// Constructor. This is the only way of setting the data members.
        /// </summary>
        public ResultFromEngine(BuildRequest request, BuildResult result)
        {
            this.request = request;
            this.result = result;
        }

        /// <summary>
        /// Request associated with the result
        /// </summary>
        public BuildRequest Request
        {
            get
            {
                return this.request;
            }
        }

        /// <summary>
        /// Build result
        /// </summary>
        public BuildResult Result
        {
            get
            {
                return this.result;
            }
        }
    }
}