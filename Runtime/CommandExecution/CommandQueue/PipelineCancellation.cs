using System;

namespace Unity.Pipeline
{
    /// <summary>
    /// <para>Cooperative cancellation for commands (CLI-335). Long-running command or
    /// <c>eval</c> code checks this periodically; when a client has requested cancellation
    /// (<c>POST /api/job/cancel</c>), the code can wind down early:</para>
    /// <code>
    /// for (int i = 0; i &lt; n; i++)
    /// {
    ///     Unity.Pipeline.PipelineCancellation.ThrowIfCancellationRequested();
    ///     // … chunk of work …
    /// }
    /// </code>
    /// <para>Arbitrary synchronous code cannot be aborted safely from outside, so a running job
    /// that never checks this flag runs to completion (its state still reports canceled).</para>
    /// <para>This is a thin static façade over the executing command (resolved via
    /// <see cref="Commands.CommandContext.Current"/>) so command code — which has no reference to
    /// "which execution is running me" — can keep
    /// calling this the same way regardless of which server dispatched it.</para>
    /// </summary>
    public static class PipelineCancellation
    {
        /// <summary>True when the currently executing command has been asked to cancel.</summary>
        public static bool IsCancellationRequested
        {
            get
            {
                return Commands.CommandContext.Current?.IsCancellationRequested ?? false;
            }
        }

        /// <summary>Throw <see cref="OperationCanceledException"/> when cancellation was requested.</summary>
        public static void ThrowIfCancellationRequested()
        {
            if (IsCancellationRequested)
            {
                throw new OperationCanceledException("Pipeline job cancellation was requested");
            }
        }
    }
}
