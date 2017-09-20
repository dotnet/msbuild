using Microsoft.Build.Construction;
using Microsoft.Build.Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Build.Evaluation
{
    class EvaluationPerformanceCounter
    {
        public struct EvaluationLocation
        {
            public readonly double EvaluationPassOrdinal;
            public readonly string EvaluationPass;
            public readonly string File;
            public readonly int? Line;
            public readonly string ElementName;
            public readonly object ElementOrCondition;

            private EvaluationLocation(double evaluationPassOrdinal, string evaluationPass, string file, int? line, string elementName, object elementOrCondition)
            {
                EvaluationPassOrdinal = evaluationPassOrdinal;
                EvaluationPass = evaluationPass;
                File = file;
                Line = line;
                ElementName = elementName;
                ElementOrCondition = elementOrCondition;
            }

            public EvaluationLocation WithEvaluationPass(double ordinal, string evaluationPass)
            {
                return new EvaluationLocation(ordinal, evaluationPass, this.File, this.Line, this.ElementName, this.ElementOrCondition);
            }

            public EvaluationLocation WithFile(string file)
            {
                return new EvaluationLocation(this.EvaluationPassOrdinal, this.EvaluationPass, file, this.Line, this.ElementName, this.ElementOrCondition);
            }

            public EvaluationLocation WithLine(int? line)
            {
                return new EvaluationLocation(this.EvaluationPassOrdinal, this.EvaluationPass, this.File, line, this.ElementName, this.ElementOrCondition);
            }

            public EvaluationLocation WithElement(ProjectElement element)
            {
                return new EvaluationLocation(this.EvaluationPassOrdinal, this.EvaluationPass, this.File, this.Line, element?.ElementName, element);
            }

            public EvaluationLocation WithElementOrCondition(string elementOrCondition)
            {
                return new EvaluationLocation(this.EvaluationPassOrdinal, this.EvaluationPass, this.File, this.Line, "Condition", elementOrCondition);
            }

            public override bool Equals(object obj)
            {
                if (obj is EvaluationLocation other)
                {
                    return EvaluationPass == other.EvaluationPass &&
                        String.Equals(File, other.File, StringComparison.OrdinalIgnoreCase) &&
                        Line == other.Line &&
                        ElementName == other.ElementName;
                }
                return false;
            }

            public override int GetHashCode()
            {
                var hashCode = 590978104;
                hashCode = hashCode * -1521134295 + base.GetHashCode();
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(EvaluationPass);
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(File);
                hashCode = hashCode * -1521134295 + EqualityComparer<int?>.Default.GetHashCode(Line);
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(ElementName);
                return hashCode;
            }

            public override string ToString()
            {
                return $"{EvaluationPass ?? string.Empty}\t{File ?? string.Empty}\t{Line?.ToString() ?? string.Empty}\t{ElementName ?? string.Empty}";
            }
        }

        Stack<EvaluationFrame> _evaluationStack = new Stack<EvaluationFrame>();

        public Dictionary<EvaluationLocation, Tuple<TimeSpan, TimeSpan, int>> TimeSpent { get; } = new Dictionary<EvaluationPerformanceCounter.EvaluationLocation, Tuple<TimeSpan, TimeSpan, int>>();

        //Dictionary<EvaluationLocation, TimeSpan> _inclusiveTimeSpent = new Dictionary<EvaluationLocation, TimeSpan>();
        //Dictionary<EvaluationLocation, TimeSpan> _exclusiveTimeSpent = new Dictionary<EvaluationLocation, TimeSpan>();

        //public Dictionary<EvaluationLocation, TimeSpan> InclusiveTimeSpent => _inclusiveTimeSpent;
        //public Dictionary<EvaluationLocation, TimeSpan> ExclusiveTimeSpent => _exclusiveTimeSpent;

        EvaluationLocation CurrentLocation
        {
            get
            {
                if (_evaluationStack.Count == 0)
                {
                    return new EvaluationLocation();
                }
                else
                {
                    return _evaluationStack.Peek().Location;
                }
            }
        }

        

        public IDisposable TrackPass(double ordinal, string pass)
        {
            return new EvaluationFrame(this, CurrentLocation.WithEvaluationPass(ordinal, pass));
        }

        public IDisposable TrackFile(string file)
        {
            return new EvaluationFrame(this, CurrentLocation.WithFile(file)
                                                            .WithLine(null)
                                                            .WithElement(null)
                                                            );
        }

        public IDisposable TrackElement(ProjectElement element)
        {
            return new EvaluationFrame(this, CurrentLocation
                                                .WithFile(element.Location.File)
                                                .WithLine(element.Location.Line)
                                                .WithElement(element));
        }

        public IDisposable TraceCondition(IElementLocation location, string condition)
        {
            return new EvaluationFrame(this, CurrentLocation
                                                .WithFile(location.File)
                                                .WithLine(location.Line)
                                                .WithElementOrCondition(condition));
        }

        class EvaluationFrame : IDisposable
        {
            EvaluationPerformanceCounter _perfCounter;
            public readonly EvaluationLocation Location;

            Stopwatch _inclusiveTime = new Stopwatch();
            Stopwatch _exclusiveTime = new Stopwatch();

            public EvaluationFrame(EvaluationPerformanceCounter perfCounter, EvaluationLocation location)
            {
                _perfCounter = perfCounter;
                Location = location;

                _inclusiveTime.Start();
                _exclusiveTime.Start();

                if (_perfCounter._evaluationStack.Count > 0)
                {
                    _perfCounter._evaluationStack.Peek()._exclusiveTime.Stop();
                }

                _perfCounter._evaluationStack.Push(this);
            }

            public void Dispose()
            {
                _inclusiveTime.Stop();
                _exclusiveTime.Stop();

                if (_perfCounter._evaluationStack.Pop() != this)
                {
                    throw new InvalidOperationException("Evaluation frame disposed out of order");
                }

                if (_perfCounter._evaluationStack.Count > 0)
                {
                    _perfCounter._evaluationStack.Peek()._exclusiveTime.Start();
                }

                //  Add elapsed times to evalution counter dictionaries
                Tuple<TimeSpan, TimeSpan, int> previousTimeSpent;
                if (!_perfCounter.TimeSpent.TryGetValue(Location, out previousTimeSpent))
                {
                    previousTimeSpent = new Tuple<TimeSpan, TimeSpan, int>(TimeSpan.Zero, TimeSpan.Zero, 0);
                }
                
                Tuple<TimeSpan, TimeSpan, int> updatedTimeSpent = new Tuple<TimeSpan, TimeSpan, int>(
                        previousTimeSpent.Item1 + _inclusiveTime.Elapsed,
                        previousTimeSpent.Item2 + _exclusiveTime.Elapsed,
                        0
                    );

                _perfCounter.TimeSpent[Location] = updatedTimeSpent;
            }
        }


    }
}
