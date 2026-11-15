using UnityEngine;

namespace ModuleFactory.SimpleUI
{
    public enum FadeStatus { Pending, Completed, Cancelled }

    /// <summary>A terminal request stays terminal even when another fade starts.</summary>
    public sealed class FadeRequest : CustomYieldInstruction
    {
        public FadeStatus Status { get; internal set; } = FadeStatus.Pending;
        public override bool keepWaiting => Status == FadeStatus.Pending;
    }
}
