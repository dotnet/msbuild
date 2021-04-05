// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Arguments for the target skipped event.
    /// </summary>
    [Serializable]
    public class TargetSkippedEventArgs : BuildMessageEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the TargetSkippedEventArgs class.
        /// </summary>
        public TargetSkippedEventArgs()
        {
        }

        /// <summary>
        /// Initializes a new instance of the TargetSkippedEventArgs class.
        /// </summary>
        public TargetSkippedEventArgs
        (
            string message,
            params object[] messageArgs
        )
            : base(
                  subcategory: null,
                  code: null,
                  file: null,
                  lineNumber: 0,
                  columnNumber: 0,
                  endLineNumber: 0,
                  endColumnNumber: 0,
                  message: message,
                  helpKeyword: null,
                  senderName: null,
                  importance: MessageImportance.Low,
                  eventTimestamp: DateTime.UtcNow,
                  messageArgs: messageArgs)
        {
        }

        /// <summary>
        /// Gets or sets the name of the target being skipped.
        /// </summary>
        public string TargetName { get; set; }

        /// <summary>
        /// Gets or sets the parent target of the target being skipped.
        /// </summary>
        public string ParentTarget { get; set; }

        /// <summary>
        /// File where this target was declared.
        /// </summary>
        public string TargetFile { get; set; }

        /// <summary>
        /// Why the parent target built this target.
        /// </summary>
        public TargetBuiltReason BuildReason { get; set; }

        public bool OriginallySucceeded { get; set; }

        public string Condition { get; set; }

        public string EvaluatedCondition { get; set; }

        internal override void WriteToStream(BinaryWriter writer)
        {
            base.WriteToStream(writer);

            writer.WriteOptionalString(TargetName);
            writer.WriteOptionalString(ParentTarget);
            writer.WriteOptionalString(TargetFile);
            writer.WriteOptionalString(Condition);
            writer.WriteOptionalString(EvaluatedCondition);
            writer.Write7BitEncodedInt((int)BuildReason);
            writer.Write(OriginallySucceeded);
        }

        internal override void CreateFromStream(BinaryReader reader, int version)
        {
            base.CreateFromStream(reader, version);

            TargetName = reader.ReadOptionalString();
            ParentTarget = reader.ReadOptionalString();
            TargetFile = reader.ReadOptionalString();
            Condition = reader.ReadOptionalString();
            EvaluatedCondition = reader.ReadOptionalString();
            BuildReason = (TargetBuiltReason)reader.Read7BitEncodedInt();
            OriginallySucceeded = reader.ReadBoolean();
        }

        public override string Message
        {
            get
            {
                if (RawMessage == null)
                {
                    lock (locker)
                    {
                        if (RawMessage == null)
                        {
                            if (Condition != null)
                            {
                                RawMessage = FormatResourceStringIgnoreCodeAndKeyword(
                                    "TargetSkippedFalseCondition",
                                    TargetName,
                                    Condition,
                                    EvaluatedCondition);
                            }
                            else
                            {
                                RawMessage = FormatResourceStringIgnoreCodeAndKeyword(
                                    OriginallySucceeded
                                    ? "TargetAlreadyCompleteSuccess"
                                    : "TargetAlreadyCompleteFailure",
                                    TargetName);
                            }
                        }
                    }
                }

                return RawMessage;
            }
        }
    }
}
