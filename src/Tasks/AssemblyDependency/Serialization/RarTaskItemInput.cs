// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Collections;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks.AssemblyDependency
{
    // Minimal shim for ProjectItemInstance.TaskItem, which is used for RAR inputs.
    // Code paths not hit by RAR are unimplmeneted and will throw an exception.
    // This allows us to emit a smaller serialization payload and optimize hot paths.
    internal class RarTaskItemInput : RarTaskItemBase
    {
        private string _evaluatedIncludeEscaped;

        private string _evaluatedIncludeUnescaped;

        public RarTaskItemInput()
            : base()
        {
            _evaluatedIncludeEscaped = string.Empty;
            _evaluatedIncludeUnescaped = string.Empty;
        }

        public RarTaskItemInput(ITaskItem taskItem)
        {
            // This should only be called with a ProjectItemInstance.TaskItem.
            if (taskItem is not ITaskItem2 taskItem2)
            {
                throw new ArgumentException("Type does not implement 'ITaskItem2'.", nameof(taskItem));
            }

            if (taskItem2.CloneCustomMetadataEscaped() is not Dictionary<string, string> metadata)
            {
                throw new ArgumentException(
                    "Implementation of 'ITaskItem2.CloneCustomMetadataEscaped()' is not of type 'Dictionary<string, string>'.",
                    nameof(taskItem));
            }

            // Store the unescaped value, as this is frequently used by RAR and is immutable.
            // TODO: How many times does this get hit?
            _evaluatedIncludeUnescaped = taskItem.ItemSpec;
            _evaluatedIncludeEscaped = taskItem2.EvaluatedIncludeEscaped;
            _metadata = metadata;
        }

        public override string ItemSpec
        {
            get => _evaluatedIncludeUnescaped;
            set => throw new NotImplementedException();
        }

        public Dictionary<string, string> Metadata => _metadata;

        public override int MetadataCount => _metadata.Count;

        public override void CopyMetadataTo(ITaskItem destinationItem)
        {
            // This should only by called with a Utilities.TaskItem.
            if (destinationItem is not IMetadataContainer metadataContainer)
            {
                throw new ArgumentException("Type does not implement 'IMetadataContainer'.", nameof(destinationItem));
            }

            metadataContainer.ImportMetadata(_metadata);
        }

        public override string GetMetadata(string metadataName) =>
            EscapingUtilities.UnescapeAll(GetMetadataValueEscaped(metadataName));

        public override string GetMetadataValueEscaped(string metadataName)
        {
            if (_metadata.TryGetValue(metadataName, out string? metadataValue))
            {
                return metadataValue;
            }

            if (FileUtilities.ItemSpecModifiers.IsItemSpecModifier(metadataName))
            {
                // Current directory is only required full full path evaluation
                // Because we cache the full path ahead of time, it will never be called.
                string? dummy = null;
                metadataValue = FileUtilities.ItemSpecModifiers.GetItemSpecModifier(
                    null,
                    _evaluatedIncludeEscaped, // TODO: Is any defining project modifier called?
                    null,
                    metadataName,
                    ref dummy);

                return metadataValue ?? string.Empty;
            }

            return string.Empty;
        }

        public override void Translate(ITranslator translator)
        {
            translator.Translate(ref _evaluatedIncludeUnescaped);
            translator.Translate(ref _evaluatedIncludeEscaped);
            base.Translate(translator);
        }
    }
}