// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Extensions.ProjectModel
{
    public static class DiagnosticMessageExtensions
    {
        /// <summary>
        /// Returns true if <paramref name="messages"/> has at least one message with <see cref="DiagnosticMessageSeverity.Error"/>.
        /// </summary>
        /// <param name="messages">Sequence of <see cref="DiagnosticMessage"/> objects.</param>
        /// <returns><c>true</c> if any messages is an error message, <c>false</c> otherwise.</returns>
        public static bool HasErrors(this IEnumerable<DiagnosticMessage> messages)
        {
            return messages.Any(m => m.Severity == DiagnosticMessageSeverity.Error);
        }
    }
}