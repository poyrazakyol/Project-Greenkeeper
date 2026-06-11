using UnityEngine;

namespace Greenkeeper.Core.Events
{
    /// <summary>Float payload channel (TDD §5.1), e.g. mow completion 0-1.</summary>
    [CreateAssetMenu(menuName = "Greenkeeper/Events/Float Channel", fileName = "OnFloatEvent")]
    public sealed class FloatEventChannelSO : EventChannelSO<float>
    {
    }
}
