// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Construction;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli;
using System.Linq;
using System.IO;
using Newtonsoft.Json;

namespace Microsoft.DotNet.ProjectJsonMigration
{
    public abstract class ConditionalTransform<T, U> : ITransform<T, U>
    {
        private Func<T, bool> _condition;

        public ConditionalTransform(Func<T,bool> condition)
        {
            _condition = condition;
        }

        public U Transform(T source)
        {
            if (_condition == null || _condition(source))
            {
                return ConditionallyTransform(source);
            }

            return default(U);
        }

        public abstract U ConditionallyTransform(T source);
    }
}
