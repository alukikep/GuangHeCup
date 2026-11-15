using System;
using System.Collections.Generic;

namespace ModuleFactory.SimpleEvents
{
    /// <summary>Dispatches typed notifications synchronously on the calling thread.</summary>
    /// <remarks>Use a single thread for all operations on an instance.</remarks>
    public sealed class EventBus
    {
        private readonly Dictionary<Type, Delegate> listeners = new Dictionary<Type, Delegate>();

        public IDisposable Subscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            var subscription = new Subscription<T>(this, handler);
            var type = typeof(T);
            listeners.TryGetValue(type, out var current);
            listeners[type] = Delegate.Combine(current, subscription.Callback);
            return subscription;
        }

        public void Publish<T>(T message) where T : struct
        {
            if (listeners.TryGetValue(typeof(T), out var snapshot))
                ((Action<T>)snapshot)(message);
        }

        private void Unsubscribe<T>(Action<T> callback) where T : struct
        {
            var type = typeof(T);
            var remaining = Delegate.Remove(listeners[type], callback);
            if (remaining == null)
                listeners.Remove(type);
            else
                listeners[type] = remaining;
        }

        private sealed class Subscription<T> : IDisposable where T : struct
        {
            private EventBus owner;
            private Action<T> handler;

            public Subscription(EventBus owner, Action<T> handler)
            {
                this.owner = owner;
                this.handler = handler;
                Callback = Invoke;
            }

            public Action<T> Callback { get; }

            public void Dispose()
            {
                if (owner == null)
                    return;

                owner.Unsubscribe(Callback);
                owner = null;
                handler = null;
            }

            private void Invoke(T message)
            {
                // An in-flight delegate snapshot can still include this subscription.
                handler?.Invoke(message);
            }
        }
    }
}
