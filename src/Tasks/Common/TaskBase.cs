// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Build.Tasks
{
    public abstract class TaskBase : Task
    {
        private Logger _logger;

        internal TaskBase(Logger logger = null)
        {
            _logger = logger;
        }

        internal new Logger Log
        {
            get
            {
                if (_logger == null)
                {
                    _logger = new LogAdapter(base.Log);
                }

                return _logger;
            }
        }

        public override bool Execute()
        {
            try
            {
                ExecuteCore();
            }
            catch (BuildErrorException e)
            {
                Log.LogError(e.Message);
            }

            return !Log.HasLoggedErrors;
        }

        protected abstract void ExecuteCore();
    }
}
