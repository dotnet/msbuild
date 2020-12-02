// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Microsoft.Build.Tasks
{
    internal sealed class RoslynCodeTaskFactoryTaskInfo : IEquatable<RoslynCodeTaskFactoryTaskInfo>
    {
        /// <summary>
        /// Gets or sets the code language of the task.
        /// </summary>
        public string CodeLanguage { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="RoslynCodeTaskFactoryCodeType"/> of the task.
        /// </summary>
        public RoslynCodeTaskFactoryCodeType CodeType { get; set; }

        /// <summary>
        /// Gets or sets the name of the task.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets a <see cref="ISet{String}"/> of namespaces to use.
        /// </summary>
        public ISet<string> Namespaces { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets an <see cref="ISet{String}"/> of assembly references.
        /// </summary>
        public ISet<string> References { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets or sets the source code of the assembly.
        /// </summary>
        public string SourceCode { get; set; }

        /// <inheritdoc cref="IEquatable{T}.Equals(T)"/>
        public bool Equals(RoslynCodeTaskFactoryTaskInfo other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return References.Equals(other.References) && String.Equals(SourceCode, other.SourceCode, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            return Equals(obj as RoslynCodeTaskFactoryTaskInfo);
        }

        public override int GetHashCode()
        {
            return 0;
        }
    }
}
