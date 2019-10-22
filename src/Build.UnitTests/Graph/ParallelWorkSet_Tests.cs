using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Graph.UnitTests
{
    public class ParallelWorkSet_Tests
    {
        private class ParallelWorkSetTestCase
        {
            internal int DegreeOfParallelism { get; set; }
            internal List<WorkItem> WorkItemsToAdd { get; set; } = new List<WorkItem>();

            internal Dictionary<string, string> ExpectedCompletedWork =
                new Dictionary<string, string>(StringComparer.Ordinal);
            internal bool ShouldExpectException { get; set; }
        }

        private struct WorkItem
        {
            internal string Key { get; set; }

            internal Func<string> WorkFunc { get; set; }
        }

        private ParallelWorkSet<string, string> _workSet;

        [Fact]
        public void GivenExceptionsOnCompletionThread_CompletesAndThrowsException()
        {
            TestParallelWorkSet(new ParallelWorkSetTestCase
            {
                DegreeOfParallelism = 0,
                WorkItemsToAdd = new List<WorkItem>
                {
                    new WorkItem
                    {
                        Key = "fooKey",
                        WorkFunc = () => throw new Exception()
                    },
                    new WorkItem
                    {
                        Key = "barKey",
                        WorkFunc = () => throw new Exception()
                    },
                    new WorkItem
                    {
                        Key = "bazKey",
                        WorkFunc = () => throw new Exception()
                    }
                },
                ShouldExpectException = true
            });
        }

        [Fact]
        public void GivenExceptionsOnWorkerThread_CompletesAndThrowsExceptions()
        {
            TestParallelWorkSet(new ParallelWorkSetTestCase
            {
                DegreeOfParallelism = Environment.ProcessorCount,
                WorkItemsToAdd = new List<WorkItem>
                {
                    new WorkItem
                    {
                        Key = "fooKey",
                        WorkFunc = () => throw new Exception()
                    },
                    new WorkItem
                    {
                        Key = "barKey",
                        WorkFunc = () => throw new Exception()
                    },
                    new WorkItem
                    {
                        Key = "bazKey",
                        WorkFunc = () => throw new Exception()
                    }
                },
                ShouldExpectException = true
            });
        }

        [Fact]
        public void GivenNoWorkItemAndMultipleWorkers_Completes()
        {
            TestParallelWorkSet(new ParallelWorkSetTestCase
            {
                DegreeOfParallelism = Environment.ProcessorCount
            });
        }

        [Fact]
        public void GivenNoWorkItemAndNoWorkers_Completes()
        {
            TestParallelWorkSet(new ParallelWorkSetTestCase());
        }

        [Fact]
        public void GivenRecursiveWorkItemsAndMultipleWorkers_Completes()
        {
            TestParallelWorkSet(new ParallelWorkSetTestCase
            {
                DegreeOfParallelism = Environment.ProcessorCount,
                WorkItemsToAdd = new List<WorkItem>
                {
                    new WorkItem
                    {
                        Key = "fooKey",
                        WorkFunc = () =>
                        {
                            _workSet.AddWork("barKey", () => "barVal");
                            return "fooVal";
                        }
                    },
                    new WorkItem
                    {
                        Key = "bazKey",
                        WorkFunc = () => "bazVal"
                    }
                },
                ExpectedCompletedWork = new Dictionary<string, string>
                {
                    { "fooKey", "fooVal" },
                    { "barKey", "barVal" },
                    { "bazKey", "bazVal" }
                }
            });
        }

        [Fact]
        public void GivenRecursiveWorkItemsAndNoWorker_Completes()
        {
            TestParallelWorkSet(new ParallelWorkSetTestCase
            {
                DegreeOfParallelism = 0,
                WorkItemsToAdd = new List<WorkItem>
                {
                    new WorkItem
                    {
                        Key = "fooKey",
                        WorkFunc = () =>
                        {
                            _workSet.AddWork("barKey", () => "barVal");
                            return "fooVal";
                        }
                    },
                    new WorkItem
                    {
                        Key = "bazKey",
                        WorkFunc = () => "bazVal"
                    }
                },
                ExpectedCompletedWork = new Dictionary<string, string>
                {
                    { "fooKey", "fooVal" },
                    { "barKey", "barVal" },
                    { "bazKey", "bazVal" }
                }
            });
        }

        [Fact]
        public void GivenWorkItemsAndMultipleWorkers_Completes()
        {
            TestParallelWorkSet(new ParallelWorkSetTestCase
            {
                DegreeOfParallelism = Environment.ProcessorCount,
                WorkItemsToAdd = new List<WorkItem>
                {
                    new WorkItem
                    {
                        Key = "fooKey",
                        WorkFunc = () => "fooVal"
                    },
                    new WorkItem
                    {
                        Key = "barKey",
                        WorkFunc = () => "barVal"
                    },
                    new WorkItem
                    {
                        Key = "bazKey",
                        WorkFunc = () => "bazVal"
                    }
                },
                ExpectedCompletedWork = new Dictionary<string, string>
                {
                    { "fooKey", "fooVal" },
                    { "barKey", "barVal" },
                    { "bazKey", "bazVal" }
                }
            });
        }

        [Fact]
        public void GivenWorkItemsAndNoWorker_Completes()
        {
            TestParallelWorkSet(new ParallelWorkSetTestCase
            {
                DegreeOfParallelism = 0,
                WorkItemsToAdd = new List<WorkItem>
                {
                    new WorkItem
                    {
                        Key = "fooKey",
                        WorkFunc = () => "fooVal"
                    },
                    new WorkItem
                    {
                        Key = "barKey",
                        WorkFunc = () => "barVal"
                    },
                    new WorkItem
                    {
                        Key = "bazKey",
                        WorkFunc = () => "bazVal"
                    }
                },
                ExpectedCompletedWork = new Dictionary<string, string>
                {
                    { "fooKey", "fooVal" },
                    { "barKey", "barVal" },
                    { "bazKey", "bazVal" }
                }
            });
        }

        private void TestParallelWorkSet(ParallelWorkSetTestCase tt)
        {
            _workSet = new ParallelWorkSet<string, string>(tt.DegreeOfParallelism, StringComparer.Ordinal, CancellationToken.None);

            foreach (WorkItem workItem in tt.WorkItemsToAdd)
            {
                _workSet.AddWork(workItem.Key, workItem.WorkFunc);
            }

            if (tt.ShouldExpectException)
            {
                Should.Throw<Exception>(() => _workSet.WaitForAllWorkAndComplete());

                return;
            }

            _workSet.WaitForAllWorkAndComplete();
            _workSet.IsCompleted.ShouldBeTrue();
            _workSet.CompletedWork.ShouldBeEquivalentTo((IReadOnlyCollection<KeyValuePair<string, string>>) tt.ExpectedCompletedWork);
        }
    }
}
