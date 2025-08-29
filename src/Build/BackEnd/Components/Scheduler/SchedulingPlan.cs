﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;

#nullable disable

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// A SchedulingPlan contains timing and relationship information for a build which has already occurred.  This data can then be
    /// used by subsequent builds to determine how best to distribute work among several nodes.
    /// </summary>
    internal class SchedulingPlan
    {
        /// <summary>
        /// The configuration cache.
        /// </summary>
        private IConfigCache _configCache;

        /// <summary>
        /// The active scheduling data.
        /// </summary>
        private SchedulingData _schedulingData;

        /// <summary>
        /// Mapping of project full paths to plan configuration data.
        /// </summary>
        private Dictionary<string, PlanConfigData> _configPathToData = new Dictionary<string, PlanConfigData>();

        /// <summary>
        /// Mapping of configuration ids to plan configuration data.
        /// </summary>
        private Dictionary<int, PlanConfigData> _configIdToData = new Dictionary<int, PlanConfigData>();

        /// <summary>
        /// Mapping of configuration ids to the set of configurations which were traversed to get to this configuration.
        /// </summary>
        private Dictionary<int, List<Stack<PlanConfigData>>> _configIdToPaths = new Dictionary<int, List<Stack<PlanConfigData>>>();

        /// <summary>
        /// Constructor.
        /// </summary>
        public SchedulingPlan(IConfigCache configCache, SchedulingData schedulingData)
        {
            _configCache = configCache;
            _schedulingData = schedulingData;
            this.MaximumConfigurationId = BuildRequestConfiguration.InvalidConfigurationId;
        }

        public PlanConfigData GetConfiguration(int configId)
        {
            _configPathToData.TryGetValue(_configCache[configId].ProjectFullPath, out PlanConfigData data);
            return data;
        }

        /// <summary>
        /// Returns true if a valid plan was read, false otherwise.
        /// </summary>
        public bool IsPlanValid
        {
            get;
            private set;
        }

        /// <summary>
        /// Returns the largest configuration id known to the plan.
        /// </summary>
        public int MaximumConfigurationId
        {
            get;
            private set;
        }

        /// <summary>
        /// Writes a plan for the specified submission id.
        /// </summary>
        public void WritePlan(int submissionId, ILoggingService loggingService, BuildEventContext buildEventContext)
        {
            if (!BuildParameters.EnableBuildPlan)
            {
                return;
            }

            SchedulableRequest rootRequest = GetRootRequest(submissionId);
            if (rootRequest == null)
            {
                return;
            }

            string planName = GetPlanName(rootRequest);
            if (String.IsNullOrEmpty(planName))
            {
                return;
            }

            try
            {
                using (StreamWriter file = new StreamWriter(File.Open(planName, FileMode.Create)))
                {
                    // Write the accumulated configuration times.
                    Dictionary<int, double> accumulatedTimeByConfiguration = new Dictionary<int, double>();
                    RecursiveAccumulateConfigurationTimes(rootRequest, accumulatedTimeByConfiguration);

                    List<KeyValuePair<int, double>> configurationsInOrder = new(accumulatedTimeByConfiguration);
                    configurationsInOrder.Sort((KeyValuePair<int, double> l, KeyValuePair<int, double> r) => Comparer<int>.Default.Compare(l.Key, r.Key));
                    foreach (KeyValuePair<int, double> configuration in configurationsInOrder)
                    {
                        file.WriteLine(String.Format(CultureInfo.InvariantCulture, "{0} {1} {2}", configuration.Key, configuration.Value, _configCache[configuration.Key].ProjectFullPath));
                    }

                    file.WriteLine();

                    // Write out the dependency information.
                    RecursiveWriteDependencies(file, rootRequest);
                }
            }
            catch (IOException)
            {
                loggingService.LogCommentFromText(buildEventContext, MessageImportance.Low, ResourceUtilities.FormatResourceStringStripCodeAndKeyword("CantWriteBuildPlan", planName));
            }
        }

        /// <summary>
        /// Reads a plan for the specified submission Id.
        /// </summary>
        public void ReadPlan(int submissionId, ILoggingService loggingService, BuildEventContext buildEventContext)
        {
            if (!BuildParameters.EnableBuildPlan)
            {
                return;
            }

            SchedulableRequest rootRequest = GetRootRequest(submissionId);
            if (rootRequest == null)
            {
                return;
            }

            string planName = GetPlanName(rootRequest);
            if (String.IsNullOrEmpty(planName))
            {
                return;
            }

            if (!FileSystems.Default.FileExists(planName))
            {
                return;
            }

            try
            {
                using (StreamReader file = new StreamReader(File.Open(planName, FileMode.Open)))
                {
                    ReadTimes(file);
                    ReadHierarchy(file);
                }

                if (_configIdToData.Count > 0)
                {
                    AnalyzeData();
                }
            }
            catch (IOException)
            {
                loggingService.LogCommentFromText(buildEventContext, MessageImportance.Low, ResourceUtilities.FormatResourceStringStripCodeAndKeyword("CantReadBuildPlan", planName));
            }
            catch (InvalidDataException)
            {
                loggingService.LogCommentFromText(buildEventContext, MessageImportance.Low, ResourceUtilities.FormatResourceStringStripCodeAndKeyword("BuildPlanCorrupt", planName));
            }
            catch (FormatException)
            {
                loggingService.LogCommentFromText(buildEventContext, MessageImportance.Low, ResourceUtilities.FormatResourceStringStripCodeAndKeyword("BuildPlanCorrupt", planName));
            }
        }

        /// <summary>
        /// Returns the config id for the config specified by the path, if any.
        /// </summary>
        /// <returns>The config id if one exists, otherwise BuildRequestConfiguration.InvalidConfigurationId</returns>
        public int GetConfigIdForPath(string configPath)
        {
            PlanConfigData config;
            if (!_configPathToData.TryGetValue(configPath, out config))
            {
                return BuildRequestConfiguration.InvalidConfigurationId;
            }

            return config.ConfigId;
        }

        /// <summary>
        /// Gets the name of the plan file for a specified submission.
        /// </summary>
        private string GetPlanName(SchedulableRequest rootRequest)
        {
            if (rootRequest == null)
            {
                return null;
            }

            return _configCache[rootRequest.BuildRequest.ConfigurationId].ProjectFullPath + ".buildplan";
        }

        /// <summary>
        /// Analyzes the plan data which has been read.
        /// </summary>
        private void AnalyzeData()
        {
            DoRecursiveAnalysis();
            if (Traits.Instance.DebugScheduler)
            {
                DetermineExpensiveConfigs();
                DetermineConfigsByNumberOfOccurrences();
                DetermineConfigsWithTheMostImmediateReferences();
                DetermineConfigsWithGreatestPlanTime();
            }

            IsPlanValid = true;
        }

        /// <summary>
        /// Writes out configuration in order of the greatest total plan time.
        /// </summary>
        private void DetermineConfigsWithGreatestPlanTime()
        {
            List<KeyValuePair<int, PlanConfigData>> projectsInOrderOfTotalPlanTime = new(_configIdToData);
            projectsInOrderOfTotalPlanTime.Sort((left, right) => Comparer<double>.Default.Compare(right.Value.TotalPlanTime, left.Value.TotalPlanTime));
            foreach (KeyValuePair<int, PlanConfigData> configuration in projectsInOrderOfTotalPlanTime)
            {
                PlanConfigData config = configuration.Value;
                Console.WriteLine("{0}: {1} ({2} referrers) {3}", configuration.Key, config.TotalPlanTime, config.ReferrerCount, config.ConfigFullPath);
                foreach (PlanConfigData referrer in config.Referrers)
                {
                    Console.WriteLine("     {0} {1}", referrer.ConfigId, referrer.ConfigFullPath);
                }

                Console.WriteLine();
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Writes out configs in order of most immediate references.
        /// </summary>
        private void DetermineConfigsWithTheMostImmediateReferences()
        {
            Console.WriteLine("Projects with the most immediate children:");
            List<KeyValuePair<int, PlanConfigData>> projectsInOrderOfImmediateChildCount = new(_configIdToData);
            projectsInOrderOfImmediateChildCount.Sort((left, right) => Comparer<int>.Default.Compare(right.Value.ReferencesCount, left.Value.ReferencesCount));
            foreach (KeyValuePair<int, PlanConfigData> configuration in projectsInOrderOfImmediateChildCount)
            {
                Console.WriteLine("{0}: {1} {2}", configuration.Key, configuration.Value.ReferencesCount, configuration.Value.ConfigFullPath);
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Writes out configs in order of how often they are seen in the hierarchy.
        /// </summary>
        private void DetermineConfigsByNumberOfOccurrences()
        {
            Console.WriteLine("Configs in hierarchy by number of occurrences:");
            List<int> projectsInOrderOfReference = new List<int>(_configIdToData.Keys);
            projectsInOrderOfReference.Sort(delegate (int left, int right) { return -Comparer<int>.Default.Compare(_configIdToPaths[left].Count, _configIdToPaths[right].Count); });
            foreach (int configId in projectsInOrderOfReference)
            {
                Console.WriteLine("{0}: {1} {2}", configId, _configIdToPaths[configId].Count, _configIdToData[configId].ConfigFullPath);
            }

            Console.WriteLine();
        }

        /// <summary>
        /// This method finds all of the paths which lead to any given project
        /// </summary>
        private void DoRecursiveAnalysis()
        {
            Stack<PlanConfigData> currentPath = new Stack<PlanConfigData>();
            PlanConfigData root = _configIdToData[1];
            RecursiveVisitNodes(root, currentPath);
        }

        /// <summary>
        /// Recursively visits all nodes in the hierarchy.
        /// </summary>
        private void RecursiveVisitNodes(PlanConfigData root, Stack<PlanConfigData> currentPath)
        {
            // Store the current path as a new path for this config
            List<Stack<PlanConfigData>> pathsForConfig;
            if (!_configIdToPaths.TryGetValue(root.ConfigId, out pathsForConfig))
            {
                pathsForConfig = new List<Stack<PlanConfigData>>();
                _configIdToPaths[root.ConfigId] = pathsForConfig;
            }

            // Reverse the stack so we get a path from the root to this node.
            Stack<PlanConfigData> pathToAdd = new Stack<PlanConfigData>(currentPath);

            // And add it to the list of paths.
            pathsForConfig.Add(pathToAdd);

            // Now add ourselves to the current path
            currentPath.Push(root);

            // Visit our children
            foreach (PlanConfigData child in root.References)
            {
                RecursiveVisitNodes(child, currentPath);
            }

            // Remove ourselves from the current path
            currentPath.Pop();
        }

        /// <summary>
        /// Finds projects in order of expense and displays the paths leading to them.
        /// </summary>
        private void DetermineExpensiveConfigs()
        {
            Console.WriteLine("Projects by expense:");

            List<PlanConfigData> projectsByExpense = new List<PlanConfigData>(_configIdToData.Values);

            // Sort most expensive to least expensive.
            projectsByExpense.Sort(delegate (PlanConfigData left, PlanConfigData right) { return -Comparer<double>.Default.Compare(left.AccumulatedTime, right.AccumulatedTime); });

            foreach (PlanConfigData config in projectsByExpense)
            {
                Console.WriteLine("{0}: {1} {2}", config.ConfigId, config.AccumulatedTime, config.ConfigFullPath);
                List<Stack<PlanConfigData>> pathsByLength = _configIdToPaths[config.ConfigId];

                // Sort the paths from shortest to longest.
                pathsByLength.Sort(delegate (Stack<PlanConfigData> left, Stack<PlanConfigData> right) { return Comparer<int>.Default.Compare(left.Count, right.Count); });
                foreach (Stack<PlanConfigData> path in pathsByLength)
                {
                    Console.Write("    ");
                    foreach (PlanConfigData pathEntry in path)
                    {
                        Console.Write(" {0}", pathEntry.ConfigId);
                    }

                    Console.WriteLine();
                }

                Console.WriteLine();
            }
        }

        /// <summary>
        /// Reads the hierarchy from a plan file.
        /// </summary>
        private void ReadHierarchy(StreamReader file)
        {
            while (!file.EndOfStream)
            {
                string line = file.ReadLine();
                if (line.Length == 0)
                {
                    return;
                }

                string[] values = line.Split(MSBuildConstants.SpaceChar);
                if (values.Length < 1)
                {
                    throw new InvalidDataException("Too few values in hierarchy");
                }

                int configId = Convert.ToInt32(values[0], CultureInfo.InvariantCulture);
                PlanConfigData parent = _configIdToData[configId];

                for (int i = 1; i < values.Length; i++)
                {
                    int childId = Convert.ToInt32(values[i], CultureInfo.InvariantCulture);
                    PlanConfigData child = _configIdToData[childId];
                    parent.AddReference(child);
                }
            }
        }

        /// <summary>
        /// Reads the accumulated time and path information for each configuration from the plan file.
        /// </summary>
        private void ReadTimes(StreamReader file)
        {
            while (!file.EndOfStream)
            {
                string line = file.ReadLine();
                if (line.Length == 0)
                {
                    return;
                }

                string[] values = line.Split(MSBuildConstants.SemicolonChar);
                if (values.Length < 3)
                {
                    throw new InvalidDataException("Too few values in build plan.");
                }

                int configId = Convert.ToInt32(values[0], CultureInfo.InvariantCulture);
                double accumulatedTime = Convert.ToDouble(values[1], CultureInfo.InvariantCulture);
                string configFullPath = values[2];

                PlanConfigData data = new PlanConfigData(configId, configFullPath, accumulatedTime);
                _configIdToData[configId] = data;
                _configPathToData[configFullPath] = data;
                MaximumConfigurationId = Math.Max(MaximumConfigurationId, configId);
            }
        }

        /// <summary>
        /// Retrieves the root request for the specified submission id.
        /// </summary>
        /// <returns>The request if one exists, otherwise null.</returns>
        private SchedulableRequest GetRootRequest(int submissionId)
        {
            foreach (SchedulableRequest request in _schedulingData.GetRequestsByHierarchy(null))
            {
                if (request.BuildRequest.SubmissionId == submissionId)
                {
                    return request;
                }
            }

            return null;
        }

        /// <summary>
        /// Writes out all of the dependencies for a specified request, recursively.
        /// </summary>
        private void RecursiveWriteDependencies(StreamWriter file, SchedulableRequest request)
        {
            file.Write(request.BuildRequest.ConfigurationId);
            foreach (SchedulableRequest child in _schedulingData.GetRequestsByHierarchy(request))
            {
                file.Write(" {0}", child.BuildRequest.ConfigurationId);
            }

            file.WriteLine();

            foreach (SchedulableRequest child in _schedulingData.GetRequestsByHierarchy(request))
            {
                RecursiveWriteDependencies(file, child);
            }
        }

        /// <summary>
        /// Recursively accumulates the amount of time spent in each configuration.
        /// </summary>
        private void RecursiveAccumulateConfigurationTimes(SchedulableRequest request, Dictionary<int, double> accumulatedTimeByConfiguration)
        {
            double accumulatedTime;

            // NOTE: Do we want to count it each time the config appears in the hierarchy?  This will inflate the 
            // cost of frequently referenced configurations.
            accumulatedTimeByConfiguration.TryGetValue(request.BuildRequest.ConfigurationId, out accumulatedTime);
            accumulatedTimeByConfiguration[request.BuildRequest.ConfigurationId] = accumulatedTime + request.GetTimeSpentInState(SchedulableRequestState.Executing).TotalMilliseconds;

            foreach (SchedulableRequest childRequest in _schedulingData.GetRequestsByHierarchy(request))
            {
                RecursiveAccumulateConfigurationTimes(childRequest, accumulatedTimeByConfiguration);
            }
        }

        /// <summary>
        /// The data associated with a config as read from a build plan.
        /// </summary>
        internal class PlanConfigData
        {
            /// <summary>
            /// The configuration id.
            /// </summary>
            private int _configId;

            /// <summary>
            /// The full path to the project.
            /// </summary>
            private string _configFullPath;

            /// <summary>
            /// The amount of time spent in the configuration.
            /// </summary>
            private double _accumulatedTime;

            /// <summary>
            /// The total time of all of the references.
            /// </summary>
            private double _accumulatedTimeOfReferences;

            /// <summary>
            /// The set of references.
            /// </summary>
            private HashSet<PlanConfigData> _references = new HashSet<PlanConfigData>();

            /// <summary>
            /// The set of referrers.
            /// </summary>
            private HashSet<PlanConfigData> _referrers = new HashSet<PlanConfigData>();

            /// <summary>
            /// Constructor.
            /// </summary>
            public PlanConfigData(int configId, string configFullPath, double accumulatedTime)
            {
                _configId = configId;
                _configFullPath = configFullPath;
                _accumulatedTime = accumulatedTime;
            }

            /// <summary>
            /// Gets the configuration id.
            /// </summary>
            public int ConfigId
            {
                get { return _configId; }
            }

            /// <summary>
            /// Gets the configuration's full path.
            /// </summary>
            public string ConfigFullPath
            {
                get { return _configFullPath; }
            }

            /// <summary>
            /// Gets the configuration's accumulated time.
            /// </summary>
            public double AccumulatedTime
            {
                get { return _accumulatedTime; }
                set { _accumulatedTime = value; }
            }

            /// <summary>
            /// Gets the configuration's accumulated time for all of its references.
            /// </summary>
            public double AccumulatedTimeOfReferences
            {
                get { return _accumulatedTimeOfReferences; }
            }

            /// <summary>
            /// Retrieves the total time for this configuration, which includes the time spent on its references.
            /// </summary>
            public double TotalPlanTime
            {
                // Count our time, plus the amount of time all of our children take.  Multiply this by the total number
                // of referrers to weight us higher the more configurations depend on us.
                get { return AccumulatedTime + AccumulatedTimeOfReferences; }
            }

            /// <summary>
            /// Retrieves the number of references this configuration has.
            /// </summary>
            public int ReferencesCount
            {
                get { return _references.Count; }
            }

            /// <summary>
            /// Retrieves the references from this configuration.
            /// </summary>
            public IEnumerable<PlanConfigData> References
            {
                get { return _references; }
            }

            /// <summary>
            /// Retrieves the number of configurations which refer to this one.
            /// </summary>
            public int ReferrerCount
            {
                get { return _referrers.Count; }
            }

            /// <summary>
            /// Retrieves the configurations which refer to this one.
            /// </summary>
            public IEnumerable<PlanConfigData> Referrers
            {
                get { return _referrers; }
            }

            /// <summary>
            /// Adds the specified configuration as a reference.
            /// </summary>
            public void AddReference(PlanConfigData reference)
            {
                if (!_references.Contains(reference))
                {
                    _references.Add(reference);

                    // My own accumulated reference time and that of all of my referrers increases as well
                    if (!reference._referrers.Contains(this))
                    {
                        reference._referrers.Add(this);
                    }

                    _accumulatedTimeOfReferences += reference.AccumulatedTime;
                    RecursivelyApplyReferenceTimeToReferrers(reference.AccumulatedTime);
                }
            }

            /// <summary>
            /// Applies the specified duration offset to the configurations which refer to this one.
            /// </summary>
            public void RecursivelyApplyReferenceTimeToReferrers(double duration)
            {
                foreach (PlanConfigData referrer in _referrers)
                {
                    referrer._accumulatedTimeOfReferences += duration;
                    referrer.RecursivelyApplyReferenceTimeToReferrers(duration);
                }
            }
        }
    }
}
