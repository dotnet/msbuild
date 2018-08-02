// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Reflection;
using System.Collections.Generic;
using Microsoft.Build.Collections;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Execution
{
    /// <summary>
    /// This class packages information about task which has been loaded from a task factory.
    /// </summary>
    internal sealed class TaskFactoryWrapper
    {
        #region Data

        /// <summary>
        /// Factory which is wrapped by the wrapper
        /// </summary>
        private ITaskFactory _taskFactory;

        /// <summary>
        /// Cache of names of required properties on this type
        /// </summary>
        private IDictionary<string, string> _namesOfPropertiesWithRequiredAttribute;

        /// <summary>
        /// Cache of names of output properties on this type
        /// </summary>
        private IDictionary<string, string> _namesOfPropertiesWithOutputAttribute;

        /// <summary>
        /// Cache of names of properties on this type whose names are ambiguous
        /// </summary>
        private IDictionary<string, string> _namesOfPropertiesWithAmbiguousMatches;

        /// <summary>
        /// Cache of PropertyInfos for this type
        /// </summary>
        private IDictionary<string, TaskPropertyInfo> _propertyInfoCache;

        /// <summary>
        /// The name of the task this factory can create.
        /// </summary>
        private string _taskName;

        /// <summary>
        /// The set of special parameters that, along with the name, contribute to the identity of 
        /// this factory.
        /// </summary>
        private IDictionary<string, string> _factoryIdentityParameters;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates an instance of this class for the given type.
        /// </summary>
        internal TaskFactoryWrapper(ITaskFactory taskFactory, LoadedType taskFactoryLoadInfo, string taskName, IDictionary<string, string> factoryIdentityParameters)
        {
            ErrorUtilities.VerifyThrowArgumentNull(taskFactory, "taskFactory");
            ErrorUtilities.VerifyThrowArgumentLength(taskName, "taskName");
            _taskFactory = taskFactory;
            _taskName = taskName;
            TaskFactoryLoadedType = taskFactoryLoadInfo;
            _factoryIdentityParameters = factoryIdentityParameters;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Load information about the task factory itself
        /// </summary>
        public LoadedType TaskFactoryLoadedType
        {
            get;
            private set;
        }

        /// <summary>
        /// The task factory wrapped by the wrapper
        /// </summary>
        public ITaskFactory TaskFactory
        {
            get
            {
                return _taskFactory;
            }
        }

        /// <summary>
        /// Gets the list of names of public instance properties that have the required attribute applied.
        /// Caches the result - since it can't change during the build.
        /// </summary>
        /// <returns></returns>
        public IDictionary<string, string> GetNamesOfPropertiesWithRequiredAttribute
        {
            get
            {
                PopulatePropertyInfoCacheIfNecessary();

                return _namesOfPropertiesWithRequiredAttribute;
            }
        }

        /// <summary>
        /// Gets the list of names of public instance properties that have the output attribute applied.
        /// Caches the result - since it can't change during the build.
        /// </summary>
        /// <returns></returns>
        public IDictionary<string, string> GetNamesOfPropertiesWithOutputAttribute
        {
            get
            {
                PopulatePropertyInfoCacheIfNecessary();

                return _namesOfPropertiesWithOutputAttribute;
            }
        }

        /// <summary>
        /// Get the name of the factory wrapped by the wrapper
        /// </summary>
        public string Name
        {
            get
            {
                return _taskFactory.FactoryName;
            }
        }

        /// <summary>
        /// The set of task identity parameters that were set on 
        /// this particular factory's UsingTask statement. 
        /// </summary>
        public IDictionary<string, string> FactoryIdentityParameters
        {
            get
            {
                return _factoryIdentityParameters;
            }
        }

        #endregion

        #region Methods.

        /// <summary>
        /// Get the cached propertyinfo of the given name
        /// </summary>
        /// <param name="propertyName">property name</param>
        /// <returns>PropertyInfo</returns>
        public TaskPropertyInfo GetProperty(string propertyName)
        {
            PopulatePropertyInfoCacheIfNecessary();

            TaskPropertyInfo propertyInfo;
            if (!_propertyInfoCache.TryGetValue(propertyName, out propertyInfo))
            {
                return null;
            }
            else
            {
                if (_namesOfPropertiesWithAmbiguousMatches.ContainsKey(propertyName))
                {
                    // See comment in PopulatePropertyInfoCache
                    throw new AmbiguousMatchException();
                }

                return propertyInfo;
            }
        }

        /// <summary>
        /// Sets the given property on the task.
        /// </summary>
        internal void SetPropertyValue(ITask task, TaskPropertyInfo property, object value)
        {
            ErrorUtilities.VerifyThrowArgumentNull(task, "task");
            ErrorUtilities.VerifyThrowArgumentNull(property, "property");

            IGeneratedTask generatedTask = task as IGeneratedTask;
            if (generatedTask != null)
            {
                generatedTask.SetPropertyValue(property, value);
            }
            else
            {
                ReflectableTaskPropertyInfo propertyInfo = (ReflectableTaskPropertyInfo)property;
                propertyInfo.Reflection.SetValue(task, value, null);
            }
        }

        /// <summary>
        /// Gets the value of a given property on the given task.
        /// </summary>
        internal object GetPropertyValue(ITask task, TaskPropertyInfo property)
        {
            ErrorUtilities.VerifyThrowArgumentNull(task, "task");
            ErrorUtilities.VerifyThrowArgumentNull(property, "property");

            IGeneratedTask generatedTask = task as IGeneratedTask;
            if (generatedTask != null)
            {
                return generatedTask.GetPropertyValue(property);
            }
            else
            {
                ReflectableTaskPropertyInfo propertyInfo = property as ReflectableTaskPropertyInfo;
                if (propertyInfo != null)
                {
                    return propertyInfo.Reflection.GetValue(task, null);
                }
                else
                {
                    ErrorUtilities.ThrowInternalError("Task does not implement IGeneratedTask and we don't have {0} either.", typeof(ReflectableTaskPropertyInfo).Name);
                    throw new InternalErrorException(); // unreachable
                }
            }
        }

        /// <summary>
        /// Determines whether a task with the given name is instantiable by this factory.
        /// </summary>
        /// <param name="taskName">Name of the task.</param>
        /// <returns>
        /// <c>true</c> if this factory can instantiate such a task; otherwise, <c>false</c>.
        /// </returns>
        internal bool IsCreatableByFactory(string taskName)
        {
            return String.Equals(_taskName, taskName, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Populate the cache of PropertyInfos for this type
        /// </summary>
        private void PopulatePropertyInfoCacheIfNecessary()
        {
            if (_propertyInfoCache == null)
            {
                _propertyInfoCache = new Dictionary<string, TaskPropertyInfo>(StringComparer.OrdinalIgnoreCase);

                // Use a HybridDictionary because these are usually very small
                _namesOfPropertiesWithRequiredAttribute = new HybridDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                _namesOfPropertiesWithOutputAttribute = new HybridDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                _namesOfPropertiesWithAmbiguousMatches = new HybridDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                bool taskTypeImplementsIGeneratedTask = typeof(IGeneratedTask).IsAssignableFrom(_taskFactory.TaskType);
                TaskPropertyInfo[] propertyInfos = _taskFactory.GetTaskParameters();

                for (int i = 0; i < propertyInfos.Length; i++)
                {
                    // If the task implements IGeneratedTask, we must use the TaskPropertyInfo the factory gives us.
                    // Otherwise, we never have to hand the TaskPropertyInfo back to the task or factory, so we replace
                    // theirs with one of our own that will allow us to cache reflection data per-property.
                    TaskPropertyInfo propertyInfo = propertyInfos[i];
                    if (!taskTypeImplementsIGeneratedTask)
                    {
                        propertyInfo = new ReflectableTaskPropertyInfo(propertyInfo, _taskFactory.TaskType);
                    }

                    try
                    {
                        _propertyInfoCache.Add(propertyInfo.Name, propertyInfo);
                    }
                    catch (ArgumentException)
                    {
                        // We have encountered a duplicate entry in our hashtable; if we had used BindingFlags.IgnoreCase this
                        // would have produced an AmbiguousMatchException. In the old code, before this cache existed,
                        // that wouldn't have been thrown unless and until the project actually tried to set this ambiguous parameter.
                        // So rather than fail here, we store a list of ambiguous names and throw later, when one of them
                        // is requested.
                        _namesOfPropertiesWithAmbiguousMatches[propertyInfo.Name] = String.Empty;
                    }

                    if (propertyInfos[i].Required)
                    {
                        // we have a require attribute defined, keep a record of that
                        _namesOfPropertiesWithRequiredAttribute[propertyInfo.Name] = String.Empty;
                    }

                    if (propertyInfos[i].Output)
                    {
                        // we have a output attribute defined, keep a record of that
                        _namesOfPropertiesWithOutputAttribute[propertyInfo.Name] = String.Empty;
                    }
                }

                // Toss the dictionaries if we can as often they are empty (at least the last three are)
                _propertyInfoCache = (_propertyInfoCache.Count == 0) ? ReadOnlyEmptyDictionary<string, TaskPropertyInfo>.Instance : _propertyInfoCache;
                _namesOfPropertiesWithRequiredAttribute = (_namesOfPropertiesWithRequiredAttribute.Count == 0) ? ReadOnlyEmptyDictionary<string, string>.Instance : _namesOfPropertiesWithRequiredAttribute;
                _namesOfPropertiesWithOutputAttribute = (_namesOfPropertiesWithOutputAttribute.Count == 0) ? ReadOnlyEmptyDictionary<string, string>.Instance : _namesOfPropertiesWithOutputAttribute;
                _namesOfPropertiesWithAmbiguousMatches = (_namesOfPropertiesWithAmbiguousMatches.Count == 0) ? ReadOnlyEmptyDictionary<string, string>.Instance : _namesOfPropertiesWithAmbiguousMatches;
            }
        }
        #endregion
    }
}
