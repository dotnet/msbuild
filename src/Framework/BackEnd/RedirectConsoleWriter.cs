// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Text;
using System.Threading;

namespace Microsoft.Build.BackEnd;

/// <remarks>
/// Disposed instances remain writable but discard output because third-party code may retain
/// <see cref="Console.Out"/> across server requests. Stale output must not reach a later client.
/// </remarks>
internal sealed class RedirectConsoleWriter : TextWriter
{
    private readonly Action<string> _writeCallback;
    private readonly Timer _timer;
    private readonly LockType _lock = new LockType();
    private readonly StringWriter _bufferWriter;
    private TextWriter _destination;
    private bool _disposed;

    public RedirectConsoleWriter(Action<string> writeCallback)
    {
        _writeCallback = writeCallback;
        _bufferWriter = new StringWriter();
        _destination = _bufferWriter;
        _timer = new Timer(TimerCallback, null, 0, 40);
    }

    public override Encoding Encoding => _bufferWriter.Encoding;

    public override void Flush()
    {
        lock (_lock)
        {
            if (!_disposed)
            {
                FlushInternal();
            }
        }
    }

    public override void Write(char value)
    {
        lock (_lock)
        {
            _destination.Write(value);
        }
    }

    public override void Write(char[]? buffer)
    {
        lock (_lock)
        {
            _destination.Write(buffer);
        }
    }

    public override void Write(char[] buffer, int index, int count)
    {
        lock (_lock)
        {
            _destination.Write(buffer, index, count);
        }
    }

    public override void Write(bool value)
    {
        lock (_lock)
        {
            _destination.Write(value);
        }
    }

    public override void Write(int value)
    {
        lock (_lock)
        {
            _destination.Write(value);
        }
    }

    public override void Write(uint value)
    {
        lock (_lock)
        {
            _destination.Write(value);
        }
    }

    public override void Write(long value)
    {
        lock (_lock)
        {
            _destination.Write(value);
        }
    }

    public override void Write(ulong value)
    {
        lock (_lock)
        {
            _destination.Write(value);
        }
    }

    public override void Write(float value)
    {
        lock (_lock)
        {
            _destination.Write(value);
        }
    }

    public override void Write(double value)
    {
        lock (_lock)
        {
            _destination.Write(value);
        }
    }

    public override void Write(decimal value)
    {
        lock (_lock)
        {
            _destination.Write(value);
        }
    }

    public override void Write(string? value)
    {
        lock (_lock)
        {
            _destination.Write(value);
        }
    }

    public override void Write(object? value)
    {
        lock (_lock)
        {
            _destination.Write(value);
        }
    }

    public override void Write(string format, object? arg0)
    {
        lock (_lock)
        {
            _destination.Write(format, arg0);
        }
    }

    public override void Write(string format, object? arg0, object? arg1)
    {
        lock (_lock)
        {
            _destination.Write(format, arg0, arg1);
        }
    }

    public override void Write(string format, object? arg0, object? arg1, object? arg2)
    {
        lock (_lock)
        {
            _destination.Write(format, arg0, arg1, arg2);
        }
    }

    public override void Write(string format, params object?[] arg)
    {
        lock (_lock)
        {
            _destination.Write(format, arg);
        }
    }

    public override void WriteLine()
    {
        lock (_lock)
        {
            _destination.WriteLine();
        }
    }

    public override void WriteLine(char value)
    {
        lock (_lock)
        {
            _destination.WriteLine(value);
        }
    }

    public override void WriteLine(decimal value)
    {
        lock (_lock)
        {
            _destination.WriteLine(value);
        }
    }

    public override void WriteLine(char[]? buffer)
    {
        lock (_lock)
        {
            _destination.WriteLine(buffer);
        }
    }

    public override void WriteLine(char[] buffer, int index, int count)
    {
        lock (_lock)
        {
            _destination.WriteLine(buffer, index, count);
        }
    }

    public override void WriteLine(bool value)
    {
        lock (_lock)
        {
            _destination.WriteLine(value);
        }
    }

    public override void WriteLine(int value)
    {
        lock (_lock)
        {
            _destination.WriteLine(value);
        }
    }

    public override void WriteLine(uint value)
    {
        lock (_lock)
        {
            _destination.WriteLine(value);
        }
    }

    public override void WriteLine(long value)
    {
        lock (_lock)
        {
            _destination.WriteLine(value);
        }
    }

    public override void WriteLine(ulong value)
    {
        lock (_lock)
        {
            _destination.WriteLine(value);
        }
    }

    public override void WriteLine(float value)
    {
        lock (_lock)
        {
            _destination.WriteLine(value);
        }
    }

    public override void WriteLine(double value)
    {
        lock (_lock)
        {
            _destination.WriteLine(value);
        }
    }

    public override void WriteLine(string? value)
    {
        lock (_lock)
        {
            _destination.WriteLine(value);
        }
    }

    public override void WriteLine(object? value)
    {
        lock (_lock)
        {
            _destination.WriteLine(value);
        }
    }

    public override void WriteLine(string format, object? arg0)
    {
        lock (_lock)
        {
            _destination.WriteLine(format, arg0);
        }
    }

    public override void WriteLine(string format, object? arg0, object? arg1)
    {
        lock (_lock)
        {
            _destination.WriteLine(format, arg0, arg1);
        }
    }

    public override void WriteLine(string format, object? arg0, object? arg1, object? arg2)
    {
        lock (_lock)
        {
            _destination.WriteLine(format, arg0, arg1, arg2);
        }
    }

    public override void WriteLine(string format, params object?[] arg)
    {
        lock (_lock)
        {
            _destination.WriteLine(format, arg);
        }
    }

    private void TimerCallback(object? state)
    {
        if (_bufferWriter.GetStringBuilder().Length > 0)
        {
            Flush();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();

            lock (_lock)
            {
                if (!_disposed)
                {
                    try
                    {
                        FlushInternal();
                    }
                    finally
                    {
                        _destination = TextWriter.Null;
                        _disposed = true;
                        _bufferWriter.Dispose();
                    }
                }
            }
        }

        base.Dispose(disposing);
    }

    private void FlushInternal()
    {
        StringBuilder buffer = _bufferWriter.GetStringBuilder();
        if (buffer.Length == 0)
        {
            return;
        }

        string captured = buffer.ToString();
        buffer.Clear();

        _writeCallback(captured);
        _bufferWriter.Flush();
    }
}
