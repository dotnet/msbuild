using Microsoft.Build.Construction;
using Microsoft.Build.Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Build.Evaluation
{
    class EvaluationPerformanceCounter
    {
        public struct EvaluationLocation
        {
            public readonly string EvaluationPass;
            public readonly string File;
            public readonly int? Line;
            public readonly string ElementName;

            private EvaluationLocation(string evaluationPass, string file, int? line, string elementName)
            {
                EvaluationPass = evaluationPass;
                File = file;
                Line = line;
                ElementName = elementName;
            }

            public EvaluationLocation WithEvaluationPass(string evaluationPass)
            {
                return new EvaluationLocation(evaluationPass, this.File, this.Line, this.ElementName);
            }

            public EvaluationLocation WithFile(string file)
            {
                return new EvaluationLocation(this.EvaluationPass, file, this.Line, this.ElementName);
            }

            public EvaluationLocation WithLine(int? line)
            {
                return new EvaluationLocation(this.EvaluationPass, this.File, line, this.ElementName);
            }

            public EvaluationLocation WithElementName(string elementName)
            {
                return new EvaluationLocation(this.EvaluationPass, this.File, this.Line, elementName);
            }

            public override bool Equals(object obj)
            {
                if (obj is EvaluationLocation other)
                {
                    return EvaluationPass == other.EvaluationPass &&
                        File == other.File &&
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

        public Dictionary<EvaluationLocation, Tuple<TimeSpan, TimeSpan>> TimeSpent { get; } = new Dictionary<EvaluationPerformanceCounter.EvaluationLocation, Tuple<TimeSpan, TimeSpan>>();

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

        

        public IDisposable TrackPass(string pass)
        {
            return new EvaluationFrame(this, CurrentLocation.WithEvaluationPass(pass));
        }

        public IDisposable TrackFile(string file)
        {
            return new EvaluationFrame(this, CurrentLocation.WithFile(file));
        }

        public IDisposable TrackElement(ProjectElement element)
        {
            return new EvaluationFrame(this, CurrentLocation
                                                .WithFile(element.Location.File)
                                                .WithLine(element.Location.Line)
                                                .WithElementName(element.ElementName));
        }

        public IDisposable TrackLocation(IElementLocation location)
        {
            return new EvaluationFrame(this, CurrentLocation
                                                .WithFile(location.File)
                                                .WithLine(location.Line));
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
                Tuple<TimeSpan, TimeSpan> previousTimeSpent;
                if (!_perfCounter.TimeSpent.TryGetValue(Location, out previousTimeSpent))
                {
                    previousTimeSpent = new Tuple<TimeSpan, TimeSpan>(TimeSpan.Zero, TimeSpan.Zero);
                }
                
                Tuple<TimeSpan, TimeSpan> updatedTimeSpent = new Tuple<TimeSpan, TimeSpan>(
                        previousTimeSpent.Item1 + _inclusiveTime.Elapsed,
                        previousTimeSpent.Item2 + _exclusiveTime.Elapsed
                    );

                _perfCounter.TimeSpent[Location] = updatedTimeSpent;
            }
        }


    }
}
