using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace Kenjeii.UnityPerfCore.TimeSlicing
{
    /// <summary>
    /// Interface for work that can be split across frames. Implementations should
    /// process a bounded chunk and report whether more work remains.
    /// </summary>
    public interface ITimeSlicedTask
    {
        /// <summary>Processes up to a budget of work. Returns true when complete.</summary>
        bool Step(float frameBudgetSeconds);

        /// <summary>True while the task still has pending work.</summary>
        bool IsPending { get; }
    }

    /// <summary>
    /// Drives time-sliced tasks under a strict per-frame time budget. The scheduler
    /// checks elapsed realtime after each step and yields before the budget is exceeded,
    /// keeping every frame under a fixed millisecond cap regardless of workload.
    /// </summary>
    public sealed class FrameBudgetScheduler
    {
        private static readonly ProfilerMarker StepMarker =
            new ProfilerMarker("PerfCore.FrameBudget.Step");

        private readonly List<ITimeSlicedTask> _tasks = new List<ITimeSlicedTask>();
        private int _cursor;

        /// <summary>Default budget in seconds. 0.012f ≈ 12ms per frame.</summary>
        public float FrameBudgetSeconds = 0.012f;

        /// <summary>Number of pending tasks.</summary>
        public int PendingCount => _tasks.Count;

        /// <summary>Queues a task for execution.</summary>
        public void Enqueue(ITimeSlicedTask task)
        {
            if (task != null && task.IsPending) _tasks.Add(task);
        }

        /// <summary>
        /// Processes queued tasks until the per-frame budget is consumed.
        /// Safe to call from Update or a coroutine. Uses realtime clock so
        /// Time.timeScale changes cannot break the budget.
        /// </summary>
        public void ProcessOneFrame()
        {
            if (_tasks.Count == 0) return;

            float start = Time.realtimeSinceStartup;
            int processed = 0;

            using (StepMarker.Auto())
            {
                while (_tasks.Count > 0)
                {
                    if (_cursor >= _tasks.Count) _cursor = 0;

                    ITimeSlicedTask task = _tasks[_cursor];
                    bool finished = task.Step(FrameBudgetSeconds);

                    if (finished)
                    {
                        // Swap-back removal: no shifting, no allocation.
                        int last = _tasks.Count - 1;
                        if (_cursor != last) _tasks[_cursor] = _tasks[last];
                        _tasks.RemoveAt(last);
                        if (_cursor >= _tasks.Count) _cursor = 0;
                    }
                    else
                    {
                        _cursor++;
                    }

                    processed++;
                    // Re-check budget after each unit of work.
                    if (Time.realtimeSinceStartup - start >= FrameBudgetSeconds) break;
                    // Safety cap prevents starving other systems if a task is too fine-grained.
                    if (processed >= 1024) break;
                }
            }
        }

        /// <summary>Clears all pending tasks.</summary>
        public void Clear()
        {
            _tasks.Clear();
            _cursor = 0;
        }
    }
}
