using UnityEngine;

namespace Greenkeeper.Core.Events
{
    /// <summary>
    /// OnDayEnded channel (TDD §5.2): raised once per "End Day" with the
    /// night report. Listeners: morning summary UI, celebration re-arm;
    /// SaveSystem (M.8) and economy (M.7) join later.
    /// </summary>
    [CreateAssetMenu(menuName = "Greenkeeper/Events/Day Ended Channel", fileName = "OnDayEnded")]
    public sealed class DayEndedEventChannelSO : EventChannelSO<DayReport>
    {
    }
}
