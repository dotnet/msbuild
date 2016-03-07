using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.Cli.Build.Framework
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public class EnvironmentAttribute : TargetConditionAttribute
    {
        private string _envVar;
        private string _expectedVal;

        public EnvironmentAttribute(string envVar, string expectedVal)
        {
            if (string.IsNullOrEmpty(envVar))
            {
                throw new ArgumentNullException("envVar");
            }

            _envVar = envVar;
            _expectedVal = expectedVal;
        }

        public override bool EvaluateCondition()
        {
            var actualVal = Environment.GetEnvironmentVariable(_envVar);

            if (string.IsNullOrEmpty(_expectedVal))
            {
                return string.IsNullOrEmpty(actualVal) ||
                       actualVal.Equals("0") ||
                       actualVal.ToLower().Equals("false");
            }

            return _expectedVal.Equals(actualVal, StringComparison.Ordinal);
        }
    }
}
