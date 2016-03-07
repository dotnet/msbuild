// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.DotNet.Tools.Test
{
    public class ReportingChannelFactory : IReportingChannelFactory
    {
        public event EventHandler<IReportingChannel> TestRunnerChannelCreated;

        public IReportingChannel CreateTestRunnerChannel()
        {
            var testRunnerChannel = ReportingChannel.ListenOn(0);

            TestRunnerChannelCreated?.Invoke(this, testRunnerChannel);

            return testRunnerChannel;
        }

        public IReportingChannel CreateAdapterChannel(int port)
        {
            return ReportingChannel.ListenOn(port);
        }
    }
}
