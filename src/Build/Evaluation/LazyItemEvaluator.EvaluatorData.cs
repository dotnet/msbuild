// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Collections;
using System;
using System.Collections.Generic;
using Microsoft.Build.BackEnd;
using Microsoft.Build.BackEnd.SdkResolution;
using Microsoft.Build.Construction;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared.FileSystem;

namespace Microsoft.Build.Evaluation
{
    internal partial class LazyItemEvaluator<P, I, M, D>
    {
        class EvaluatorData : IEvaluatorData<P, I, M, D>
        {
            IEvaluatorData<P, I, M, D> _wrappedData;
            Func<string, ICollection<I>> _itemGetter;

            public EvaluatorData(IEvaluatorData<P, I, M, D> wrappedData, Func<string, ICollection<I>> itemGetter)
            {
                _wrappedData = wrappedData;
                _itemGetter = itemGetter;
            }

            public ItemDictionary<I> Items => throw new NotImplementedException();

            public List<ProjectItemElement> EvaluatedItemElements => throw new NotImplementedException();

            public ICollection<I> GetItems(string itemType)
            {
                return _itemGetter(itemType);
            }


            public IDictionary<string, List<TargetSpecification>> AfterTargets
            {
                get => _wrappedData.AfterTargets;

                set => _wrappedData.AfterTargets = value;
            }

            public IDictionary<string, List<TargetSpecification>> BeforeTargets
            {
                get => _wrappedData.BeforeTargets;

                set => _wrappedData.BeforeTargets = value;
            }

            public Dictionary<string, List<string>> ConditionedProperties => _wrappedData.ConditionedProperties;

            public List<string> DefaultTargets
            {
                get => _wrappedData.DefaultTargets;

                set => _wrappedData.DefaultTargets = value;
            }

            public int EvaluationId
            {
                get => _wrappedData.EvaluationId;
                set => _wrappedData.EvaluationId = value;
            }

            public string Directory => _wrappedData.Directory;

            public string ExplicitToolsVersion => _wrappedData.ExplicitToolsVersion;

            public PropertyDictionary<ProjectPropertyInstance> GlobalPropertiesDictionary => _wrappedData.GlobalPropertiesDictionary;

            public PropertyDictionary<ProjectPropertyInstance> EnvironmentVariablePropertiesDictionary => _wrappedData.EnvironmentVariablePropertiesDictionary;

            public ISet<string> GlobalPropertiesToTreatAsLocal => _wrappedData.GlobalPropertiesToTreatAsLocal;

            public List<string> InitialTargets
            {
                get => _wrappedData.InitialTargets;

                set => _wrappedData.InitialTargets = value;
            }

            public IEnumerable<D> ItemDefinitionsEnumerable => _wrappedData.ItemDefinitionsEnumerable;


            public bool CanEvaluateElementsWithFalseConditions => _wrappedData.CanEvaluateElementsWithFalseConditions;

            public PropertyDictionary<P> Properties => _wrappedData.Properties;

            public bool ShouldEvaluateForDesignTime => _wrappedData.ShouldEvaluateForDesignTime;

            public string SubToolsetVersion => _wrappedData.SubToolsetVersion;

            public TaskRegistry TaskRegistry
            {
                get => _wrappedData.TaskRegistry;

                set => _wrappedData.TaskRegistry = value;
            }

            public Toolset Toolset => _wrappedData.Toolset;

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

            public P GetProperty(string name, int startIndex, int endIndex)
            {
                return _wrappedData.GetProperty(name, startIndex, endIndex);
            }

            public ProjectTargetInstance GetTarget(string targetName)
            {
                return _wrappedData.GetTarget(targetName);
            }

            public void InitializeForEvaluation(IToolsetProvider toolsetProvider, IFileSystem fileSystem)
            {
                _wrappedData.InitializeForEvaluation(toolsetProvider, fileSystem);
            }

            public void RecordImport(ProjectImportElement importElement, ProjectRootElement import, int versionEvaluated, SdkResult sdkResult)
            {
                _wrappedData.RecordImport(importElement, import, versionEvaluated, sdkResult);
            }

            public void RecordImportWithDuplicates(ProjectImportElement importElement, ProjectRootElement import, int versionEvaluated)
            {
                _wrappedData.RecordImportWithDuplicates(importElement, import, versionEvaluated);
            }

            public P SetProperty(ProjectPropertyElement propertyElement, string evaluatedValueEscaped)
            {
                return _wrappedData.SetProperty(propertyElement, evaluatedValueEscaped);
            }

            public P SetProperty(string name, string evaluatedValueEscaped, bool isGlobalProperty, bool mayBeReserved, bool isEnvironmentVariable = false)
            {
                return _wrappedData.SetProperty(name, evaluatedValueEscaped, isGlobalProperty, mayBeReserved);
            }
        }
    }

    
}
