// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Build.Framework.Telemetry;

internal class BuildCheckTelemetry
{
    internal void AddCustomCheckLoadingFailure()
    { }
}

////internal class BuildCheckTelemetry : TelemetryBase
////{
////    public override string EventName => "buildcheck";

////    internal void AddCustomCheckLoadingFailure()
////    {

////    }

////    /// <summary>
////    /// True if terminal logger was used.
////    /// </summary>
////    public bool IsBuildCheckOn { get; set; }
////    public int RulesCount { get; set; }
////    public int CustomRulesCount { get; set; }
////    public int ViolationsCount { get; set; }
////    public TimeSpan TotalRuntime { get; set; }



////    public override IDictionary<string, string> GetProperties() => throw new NotImplementedException();

////    internal class BuildCheckRuleTelemetryData
////    {
////        public string RuleId { get; set; }
////        public bool IsBuiltIn { get; set; }
////        public byte DefaultSeverityId { get; set; }
////        public string DefaultSeverityName { get; set; }
////        public int ProjectsCount { get; set; }
////        public byte? ExplicitSeverityId { get; set; }
////        public string? ExplicitSeverityName { get; set; }
////        public int ViolationCount { get; set; }
////        public bool IsThrottled { get; set; }
////        public TimeSpan TotalRuntime { get; set; }
////    }

////    internal class CustomCheckErrorTelemetryData
////    {
////        public string RuleId { get; set; }
////        public string ExceptionType { get; set; }
////        public string ExceptionMessage { get; set; }
////    }
////}


