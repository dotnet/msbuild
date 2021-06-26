// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.s

using System;
using Microsoft.DotNet.ApiCompatibility.Abstractions;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    public class RuleRunnerFactory : IRuleRunnerFactory
    {
        private readonly bool _strictMode;
        private readonly string _leftName;
        private readonly string[] _rightNames;
        private RuleRunner _runner;

        public RuleRunnerFactory(string leftName, string[] rightNames, bool strictMode = false)
        {
            _strictMode = strictMode;
            _leftName = string.IsNullOrEmpty(leftName) ? RuleRunner.DEFAULT_LEFT_NAME : leftName;
            _rightNames = rightNames ?? new string[] { RuleRunner.DEFAULT_RIGHT_NAME };

            if (_rightNames.Length <= 0)
            {
                throw new ArgumentException(nameof(rightNames), Resources.RightNamesAtLeastOne);
            }

            InitializeRightNamesIfNeeded();
        }

        private void InitializeRightNamesIfNeeded()
        {
            for (int i = 0; i < _rightNames.Length; i++)
            {
                if (string.IsNullOrEmpty(_rightNames[i]))
                {
                    _rightNames[i] = RuleRunner.DEFAULT_RIGHT_NAME;
                }
            }
        }

        public IRuleRunner GetRuleRunner()
        {
            if (_runner == null)
                _runner = new RuleRunner(_leftName, _rightNames, _strictMode);

            return _runner;
        }
    }
}
