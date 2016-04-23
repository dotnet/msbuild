using System;
using Mutant.Chicken.Abstractions;

namespace dotnet_new3
{
    internal static class Disposable
    {
        public static IDisposable<T> WithDispose<T>(this T result, Action<IDisposable<T>, bool> dispose)
        {
            return new Disposable<T>(result, dispose);
        }

        public static IDisposable<T> NoDispose<T>(this T result)
        {
            return new Disposable<T>(result, (t, v) => { });
        }
    }

    internal class Disposable<T> : IDisposable<T>
    {
        private Action<IDisposable<T>, bool> _dispose;

        public T Value { get; }

        public Disposable(T result, Action<IDisposable<T>, bool> dispose)
        {
            Value = result;
            _dispose = dispose;
        }

        public void Dispose()
        {
            _dispose?.Invoke(this, true);
            _dispose = null;
            GC.SuppressFinalize(this);
        }

        ~Disposable()
        {
            _dispose?.Invoke(this, false);
            _dispose = null;
        }
    }
}
