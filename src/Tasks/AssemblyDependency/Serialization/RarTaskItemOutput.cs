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
    // Minimal shim for Utilities.TaskItem, which is used for RAR outputs.
    // Code paths not hit by RAR are unimplmeneted and will throw an exception.
    // This allows us to emit a smaller serialization payload and optimize hot paths.
    internal class RarTaskItemOutput : RarTaskItemBase
    {
        private bool _isCopyLocalFile;

        private string _evaluatedIncludeEscaped;

        public RarTaskItemOutput()
            : base()
        {
            _evaluatedIncludeEscaped = string.Empty;
        }

        public RarTaskItemOutput(ITaskItem taskItem, bool isCopyLocalFile)
        {
            // This should only be called with a Utilities.TaskItem.
            if (taskItem is not ITaskItem2 taskItem2)
            {
                throw new ArgumentException("Type does not implement 'ITaskItem2'.", nameof(taskItem));
            }

            _isCopyLocalFile = isCopyLocalFile;
            _evaluatedIncludeEscaped = taskItem2.EvaluatedIncludeEscaped;
            _metadata = new Dictionary<string, string>(taskItem2.MetadataCount);

            foreach (DictionaryEntry metadataNameWithValue in taskItem2.CloneCustomMetadataEscaped())
            {
                _metadata[(string)metadataNameWithValue.Key!] = (string)metadataNameWithValue.Value!;
            }
        }

        public override string EvaluatedIncludeEscaped
        {
            get => _evaluatedIncludeEscaped;
            set => throw new NotImplementedException();
        }

        public bool IsCopyLocalFile => _isCopyLocalFile;

        public override void CopyMetadataTo(ITaskItem destinationItem)
        {
            // This should only be called with a Utilities.TaskItem.
            if (destinationItem is not IMetadataContainer metadataContainer)
            {
                throw new ArgumentException("Type does not implement 'IMetadataContainer'.", nameof(destinationItem));
            }

            metadataContainer.ImportMetadata(_metadata);
        }

        public override string GetMetadataValueEscaped(string metadataName)
        {
            if (!metadataName.Equals(FileUtilities.ItemSpecModifiers.DefiningProjectFullPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new NotImplementedException();
            }

            // TaskItems created by RAR do not have a defining project.
            return string.Empty;
        }

        public override void Translate(ITranslator translator)
        {
            translator.Translate(ref _isCopyLocalFile);
            translator.Translate(ref _evaluatedIncludeEscaped);
            base.Translate(translator);
        }
    }
}
