// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.Build.BackEnd;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.BackEnd.SdkResolution;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Execution;

#nullable disable

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
{
    private class EvaluatorData : IEvaluatorData<P, I, M, D>
    {
        private readonly IEvaluatorData<P, I, M, D> _wrappedData;
        private readonly IReadOnlyDictionary<string, LazyItemList> _currentItemLists;
        private readonly ItemListBookmarks _itemListBookmarks;

        public EvaluatorData(IEvaluatorData<P, I, M, D> wrappedData, IReadOnlyDictionary<string, LazyItemList> currentItemLists)
        {
            _wrappedData = wrappedData;
            _currentItemLists = currentItemLists;
        }

        public EvaluatorData(IEvaluatorData<P, I, M, D> wrappedData, ItemListBookmarks itemListBookmarks)
        {
            _wrappedData = wrappedData;
            _itemListBookmarks = itemListBookmarks;
        }

        public IItemDictionary<I> Items => throw new NotImplementedException();

        public List<ProjectItemElement> EvaluatedItemElements => throw new NotImplementedException();

        public ICollection<I> GetItems(string itemType)
        {
            // Conditions see the live lists; operations see only the lists captured at their declaration.
            bool found = _itemListBookmarks is not null
                ? _itemListBookmarks.TryGetValue(itemType, out LazyItemList items)
                : _currentItemLists.TryGetValue(itemType, out items);

            return found
                ? items.GetFilteredItems(laterRemoveGlobPatterns: ImmutableHashSet<string>.Empty)
                : Array.Empty<I>();
        }

        public IDictionary<string, List<TargetSpecification>> AfterTargets
        {
            get
            {
                return _wrappedData.AfterTargets;
            }

            set
            {
                _wrappedData.AfterTargets = value;
            }
        }

        public IDictionary<string, List<TargetSpecification>> BeforeTargets
        {
            get
            {
                return _wrappedData.BeforeTargets;
            }

            set
            {
                _wrappedData.BeforeTargets = value;
            }
        }

        public Dictionary<string, List<string>> ConditionedProperties => _wrappedData.ConditionedProperties;

        public List<string> DefaultTargets
        {
            get
            {
                return _wrappedData.DefaultTargets;
            }

            set
            {
                _wrappedData.DefaultTargets = value;
            }
        }

        public int EvaluationId
        {
            get { return _wrappedData.EvaluationId; }
            set { _wrappedData.EvaluationId = value; }
        }

        public string Directory => _wrappedData.Directory;

        public string ExplicitToolsVersion => _wrappedData.ExplicitToolsVersion;

        public PropertyDictionary<ProjectPropertyInstance> GlobalPropertiesDictionary => _wrappedData.GlobalPropertiesDictionary;

        public PropertyDictionary<ProjectPropertyInstance> EnvironmentVariablePropertiesDictionary => _wrappedData.EnvironmentVariablePropertiesDictionary;

        public ISet<string> GlobalPropertiesToTreatAsLocal => _wrappedData.GlobalPropertiesToTreatAsLocal;

        public List<string> InitialTargets
        {
            get
            {
                return _wrappedData.InitialTargets;
            }

            set
            {
                _wrappedData.InitialTargets = value;
            }
        }

        public IEnumerable<D> ItemDefinitionsEnumerable => _wrappedData.ItemDefinitionsEnumerable;


        public bool CanEvaluateElementsWithFalseConditions => _wrappedData.CanEvaluateElementsWithFalseConditions;

        public PropertyDictionary<P> Properties => _wrappedData.Properties;

        public bool ShouldEvaluateForDesignTime => _wrappedData.ShouldEvaluateForDesignTime;

        public string SubToolsetVersion => _wrappedData.SubToolsetVersion;

        public TaskRegistry TaskRegistry
        {
            get
            {
                return _wrappedData.TaskRegistry;
            }

            set
            {
                _wrappedData.TaskRegistry = value;
            }
        }

        public Toolset Toolset => _wrappedData.Toolset;

        public PropertyDictionary<ProjectPropertyInstance> SdkResolvedEnvironmentVariablePropertiesDictionary => _wrappedData.SdkResolvedEnvironmentVariablePropertiesDictionary;

        public void AddSdkResolvedEnvironmentVariable(string name, string value) => throw new NotSupportedException();

        public void AddItem(I item)
        {
            throw new NotSupportedException();
        }

        public IItemDefinition<M> AddItemDefinition(string itemType)
        {
            throw new NotSupportedException();
        }

        public void AddItemIgnoringCondition(I item)
        {
            throw new NotSupportedException();
        }

        public void AddTarget(ProjectTargetInstance target)
        {
            throw new NotSupportedException();
        }

        public void AddToAllEvaluatedItemDefinitionMetadataList(M itemDefinitionMetadatum)
        {
            throw new NotSupportedException();
        }

        public void AddToAllEvaluatedItemsList(I item)
        {
            throw new NotSupportedException();
        }

        public void AddToAllEvaluatedPropertiesList(P property)
        {
            throw new NotSupportedException();
        }

        public bool EvaluateCondition(string condition)
        {
            throw new NotSupportedException();
        }

        public string ExpandString(string unexpandedValue)
        {
            throw new NotSupportedException();
        }

        public void FinishEvaluation()
        {
            _wrappedData.FinishEvaluation();
        }

        public IItemDefinition<M> GetItemDefinition(string itemType)
        {
            return _wrappedData.GetItemDefinition(itemType);
        }

        public P GetProperty(string name)
        {
            return _wrappedData.GetProperty(name);
        }

        public P GetProperty(string name, int startIndex, int length)
        {
            return _wrappedData.GetProperty(name, startIndex, length);
        }

        public ProjectTargetInstance GetTarget(string targetName)
        {
            return _wrappedData.GetTarget(targetName);
        }

        public void InitializeForEvaluation(IToolsetProvider toolsetProvider, EvaluationContext evaluationContext, LoggingContext loggingContext)
        {
            _wrappedData.InitializeForEvaluation(toolsetProvider, evaluationContext, loggingContext);
        }

        public void RecordImport(ProjectImportElement importElement, ProjectRootElement import, int versionEvaluated, SdkResult sdkResult)
        {
            _wrappedData.RecordImport(importElement, import, versionEvaluated, sdkResult);
        }

        public void RecordImportWithDuplicates(ProjectImportElement importElement, ProjectRootElement import, int versionEvaluated)
        {
            _wrappedData.RecordImportWithDuplicates(importElement, import, versionEvaluated);
        }

        public P SetProperty(ProjectPropertyElement propertyElement, string evaluatedValueEscaped, BackEnd.Logging.LoggingContext loggingContext)
        {
            return _wrappedData.SetProperty(propertyElement, evaluatedValueEscaped, loggingContext);
        }

        public P SetProperty(string name, string evaluatedValueEscaped, bool isGlobalProperty, bool mayBeReserved, LoggingContext loggingContext, bool isEnvironmentVariable = false, bool isCommandLineProperty = false)
        {
            return _wrappedData.SetProperty(name, evaluatedValueEscaped, isGlobalProperty, mayBeReserved, loggingContext: loggingContext, isCommandLineProperty);
        }
    }
}
