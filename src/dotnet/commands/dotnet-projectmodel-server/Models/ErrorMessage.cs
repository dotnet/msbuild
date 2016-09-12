// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.DotNet.ProjectModel.Server.Models
{
    public class ErrorMessage
    {
        public string Message { get; set; }
        public string Path { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }

        public override bool Equals(object obj)
        {
            var payload = obj as ErrorMessage;
            return payload != null &&
                   string.Equals(Message, payload.Message, StringComparison.Ordinal) &&
                   string.Equals(Path, payload.Path, StringComparison.OrdinalIgnoreCase) &&
                   Line == payload.Line &&
                   Column == payload.Column;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}
