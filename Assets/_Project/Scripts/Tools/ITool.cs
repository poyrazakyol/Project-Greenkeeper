using System;
using Greenkeeper.Pitch;
using UnityEngine;

namespace Greenkeeper.Tools
{
    /// <summary>
    /// Tool contract (TDD §4.1, signature verbatim). Tools are plain classes:
    /// the player-side controller owns the lifecycle and feeds a ToolContext
    /// per call, which keeps every tool testable without a scene.
    /// </summary>
    public interface ITool
    {
        ToolDefinitionSO Definition { get; }
        void OnEquip(ToolContext ctx);
        void OnUseHeld(ToolContext ctx, float dt);   // trigger held
        void OnUseReleased(ToolContext ctx);
        void OnUnequip();
    }

    /// <summary>
    /// Everything a tool may touch (TDD §4.1): player pose, look ray, the CPU
    /// grid write API and the GPU stamp queue. The stamp sink is a delegate so
    /// tests can capture stamps without a PitchRenderBridge. Dual-layer rule:
    /// tools write BOTH layers from one stroke — no GPU readback (TDD §3.1).
    /// </summary>
    public readonly struct ToolContext
    {
        public Vector3 PlayerPosition { get; }
        public Vector3 PlayerForward { get; }
        public Ray LookRay { get; }
        public PitchGrid Grid { get; }
        public Action<StampRequest> EnqueueStamp { get; }

        public ToolContext(Vector3 playerPosition, Vector3 playerForward, Ray lookRay,
            PitchGrid grid, Action<StampRequest> enqueueStamp)
        {
            PlayerPosition = playerPosition;
            PlayerForward = playerForward;
            LookRay = lookRay;
            Grid = grid;
            EnqueueStamp = enqueueStamp;
        }
    }
}
