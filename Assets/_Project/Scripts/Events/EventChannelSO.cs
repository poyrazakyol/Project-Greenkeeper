using System;
using UnityEngine;

namespace Greenkeeper.Core.Events
{
    /// <summary>
    /// Base of all event channel assets (TDD §5.1). Systems communicate only
    /// through these SO channels — never via direct cross-system references.
    /// First consumer: mow progress (Tools side raises, Debug HUD listens).
    /// </summary>
    public abstract class EventChannelSO<T> : ScriptableObject
    {
        private event Action<T> _listeners;

        public void Raise(T payload) => _listeners?.Invoke(payload);

        public void Subscribe(Action<T> listener) => _listeners += listener;

        public void Unsubscribe(Action<T> listener) => _listeners -= listener;
    }
}
