// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Tools.Test
{
    public class ReportingChannelFactory : IReportingChannelFactory
    {
        public IReportingChannel CreateChannelWithAnyAvailablePort()
        {
            return ReportingChannel.ListenOn(0);
        }

        public IReportingChannel CreateChannelWithPort(int port)
        {
            return ReportingChannel.ListenOn(port);
        }
    }
}
