using UnityEngine;

namespace Kenjeii.UnityPerfCore.CentralizedUpdate
{
    /// <summary>
    /// Immutable, shared frame configuration. One instance is shared by thousands of
    /// animators. Contains no runtime state — this is the flyweight half of the pattern.
    /// </summary>
    public sealed class SpriteFrameClip
    {
        /// <summary>Shared sprite frames. Never modified at runtime.</summary>
        public readonly Sprite[] Frames;

        /// <summary>Per-frame duration in seconds. Length matches Frames.</summary>
        public readonly float[] FrameDurations;

        /// <summary>Frames that fire an event. Length matches Frames; 0 means no event.</summary>
        public readonly int[] TriggerFrames;

        /// <summary>Whether the clip loops.</summary>
        public readonly bool Loop;

        /// <summary>Creates a shared clip definition.</summary>
        public SpriteFrameClip(Sprite[] frames, float[] frameDurations, int[] triggerFrames = null, bool loop = true)
        {
            Frames = frames ?? System.Array.Empty<Sprite>();
            FrameDurations = frameDurations ?? System.Array.Empty<float>();
            TriggerFrames = triggerFrames;
            Loop = loop;
        }
    }

    /// <summary>
    /// Lightweight per-instance animation state. Holds only an index and timer —
    /// all heavy data lives in the shared <see cref="SpriteFrameClip"/>.
    /// Driven by a centralized tick instead of an Animator or per-object Update.
    /// </summary>
    public struct SpriteFrameState
    {
        /// <summary>Current frame index.</summary>
        public int FrameIndex;

        /// <summary>Time accumulated in the current frame.</summary>
        public float Timer;

        /// <summary>True once a non-looping clip has finished.</summary>
        public bool Finished;

        /// <summary>Resets state to the first frame.</summary>
        public void Reset()
        {
            FrameIndex = 0;
            Timer = 0f;
            Finished = false;
        }
    }

    /// <summary>
    /// C# frame-driven sprite animation. Bypasses the Animator component entirely —
    /// no AnimatorController, no PlayableGraph, no per-object Update.
    /// </summary>
    public static class SpriteFrameAnimator
    {
        /// <summary>
        /// Advances one instance. Returns the frame index that just became active,
        /// or -1 if no frame changed (zero allocation on every path).
        /// </summary>
        public static int Advance(
            ref SpriteFrameState state,
            SpriteFrameClip clip,
            SpriteRenderer renderer,
            float deltaTime)
        {
            if (clip == null || clip.Frames.Length == 0 || renderer == null || state.Finished) return -1;

            state.Timer += deltaTime;
            int length = clip.Frames.Length;
            int enteredFrame = -1;

            // while guards against very large deltaTime skipping several frames.
            while (state.Timer >= GetFrameDuration(clip, state.FrameIndex))
            {
                state.Timer -= GetFrameDuration(clip, state.FrameIndex);
                int next = state.FrameIndex + 1;

                if (next >= length)
                {
                    if (!clip.Loop)
                    {
                        state.Finished = true;
                        state.Timer = 0f;
                        return -1;
                    }
                    next = 0;
                }

                state.FrameIndex = next;
                enteredFrame = next;
            }

            renderer.sprite = clip.Frames[state.FrameIndex];
            return enteredFrame;
        }

        /// <summary>True when the given frame is a trigger frame.</summary>
        public static bool IsTriggerFrame(SpriteFrameClip clip, int frameIndex)
        {
            if (clip == null || clip.TriggerFrames == null) return false;
            int[] triggers = clip.TriggerFrames;
            for (int i = 0; i < triggers.Length; i++)
            {
                if (triggers[i] == frameIndex) return true;
            }
            return false;
        }

        /// <summary>Applies the first frame immediately (used on spawn).</summary>
        public static void ApplyFirst(ref SpriteFrameState state, SpriteFrameClip clip, SpriteRenderer renderer)
        {
            state.Reset();
            if (clip != null && clip.Frames.Length > 0 && renderer != null)
            {
                renderer.sprite = clip.Frames[0];
            }
        }

        private static float GetFrameDuration(SpriteFrameClip clip, int frameIndex)
        {
            if (clip.FrameDurations.Length == 0) return 1f / 12f;
            if (frameIndex < clip.FrameDurations.Length) return clip.FrameDurations[frameIndex];
            return clip.FrameDurations[clip.FrameDurations.Length - 1];
        }
    }
}
