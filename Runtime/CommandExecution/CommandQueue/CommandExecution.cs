using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
#if UNITY_6000_5_OR_NEWER
using Unity.Scripting.LifecycleManagement;
#endif

namespace Unity.Pipeline.Commands
{
    /// <summary>Where an execution stands. An ended one's result says how it ended.</summary>
    enum CommandExecutionState
    {
        Queued = 0,
        Running = 1,
        Ended = 2
    }

    /// <summary>
    /// One execution of a command, live and read-only: each member reads where it stands when it
    /// is read. Its id and command; its state and, once it ended, its result; whether its
    /// cancellation was asked for; and its progress while it runs. Awaiting it completes with its
    /// result once it ended, on the kind of thread that awaits, main or background; it can be
    /// awaited any number of times. Whoever holds it can cancel it.
    /// </summary>
    /// <remarks>
    /// Thread-safe. Each field is set before <see cref="State"/> moves past it, so a reader that
    /// reads State first sees the rest consistent with it.
    /// </remarks>
#if UNITY_6000_5_OR_NEWER
    [NoAutoStaticsCleanup]
#endif
    sealed class CommandExecution
    {
        readonly CliProgressState m_Progress;
        volatile int m_State;

        /// <summary>
        /// Cancelled by <see cref="Cancel"/>. Never disposed: it has no timer and no wait handle, so
        /// a Cancel after it ended has nothing to fail on.
        /// </summary>
        readonly CancellationTokenSource m_Cancellation = new CancellationTokenSource();

        /// <summary>
        /// Completes once it ended and its report was made. Continuations run asynchronously, so
        /// an awaiter never runs inside the queue's own code.
        /// </summary>
        readonly TaskCompletionSource<CommandResult> m_Completion = new (TaskCreationOptions.RunContinuationsAsynchronously);

        /// <param name="id">Its id.</param>
        /// <param name="command">The full name of the command it runs.</param>
        /// <param name="parameters">Its arguments as the executor bound them, by parameter name.</param>
        /// <param name="progress">The progress of whichever execution runs now.</param>
        internal CommandExecution(CommandExecutionId id, string command, IReadOnlyDictionary<string, object> parameters,
            CliProgressState progress)
        {
            Id = id;
            Command = command;
            Parameters = parameters ?? s_NoParameters;
            m_Progress = progress;
            EnqueuedAt = DateTime.UtcNow;
        }

        /// <summary>Its id, unique within the instance.</summary>
        public CommandExecutionId Id { get; }

        /// <summary>The full name of the command it runs.</summary>
        public string Command { get; }

        /// <summary>
        /// Its arguments as the executor bound them, by parameter name, before they were converted
        /// to the parameters' types: what a transport echoes and the report carries. Each a string,
        /// boolean, number, date or null, or a list or dictionary of them for a structured value.
        /// </summary>
        internal IReadOnlyDictionary<string, object> Parameters { get; }

        static readonly IReadOnlyDictionary<string, object> s_NoParameters = new Dictionary<string, object>();

        /// <summary>Where it stands.</summary>
        public CommandExecutionState State => (CommandExecutionState)m_State;

        /// <summary>How it ended; null until it has.</summary>
        public CommandResult? Result { get; private set; }

        /// <summary>Whether a client asked it to stop.</summary>
        public bool CancellationRequested => m_Cancellation.IsCancellationRequested;

        /// <summary>
        /// Cancelled once a client asks it to stop, for the queue to stop waiting for its turn at
        /// once rather than when it comes.
        /// </summary>
        internal CancellationToken CancellationToken => m_Cancellation.Token;

        /// <summary>Its progress while it runs, once it or the Editor reported any; null otherwise.</summary>
        public CommandProgress Progress => State == CommandExecutionState.Running ? CommandProgress.From(m_Progress.Current) : null;

        /// <summary>When it was queued, in UTC.</summary>
        public DateTime EnqueuedAt { get; }

        /// <summary>When it started, in UTC; null until it has.</summary>
        public DateTime? StartedAt { get; private set; }

        /// <summary>When it ended, in UTC; null until it has.</summary>
        public DateTime? EndedAt { get; private set; }

        /// <summary>
        /// Completes with its result once it ended and its report was made, however it is awaited:
        /// while it waits, while it runs, or after it ended.
        /// </summary>
        public TaskAwaiter<CommandResult> GetAwaiter() => m_Completion.Task.GetAwaiter();

        /// <summary>Lets its awaiters go on, once it ended and its report was made.</summary>
        internal void Complete(CommandResult result)
        {
            m_Completion.TrySetResult(result);
        }

        /// <summary>
        /// Ends it without waiting for its turn if it hasn't started, and then it never does, or
        /// asks it to stop if it runs. A running command stops only when its code checks, through
        /// PipelineCancellation: arbitrary synchronous main-thread code cannot be aborted safely.
        /// </summary>
        /// <returns>False when it has already ended.</returns>
        public bool Cancel()
        {
            if (State == CommandExecutionState.Ended)
                return false;

            m_Cancellation.Cancel();
            return true;
        }

        internal void MarkRunning()
        {
            StartedAt = DateTime.UtcNow;
            m_State = (int)CommandExecutionState.Running;
        }

        internal void MarkEnded(CommandResult result)
        {
            Result = result;
            EndedAt = DateTime.UtcNow;
            m_State = (int)CommandExecutionState.Ended;
        }
    }

    /// <summary>
    /// The progress of the running command: what it last reported itself or, until it reports,
    /// the Editor's own progress, such as an import it set off. What it does, and how far it is,
    /// as a count, a fraction, or both.
    /// </summary>
    sealed class CommandProgress
    {
        CommandProgress(string title, string info, long? current, long? total, double? fraction)
        {
            Title = title;
            Info = info;
            Current = current;
            Total = total;
            Fraction = fraction;
        }

        public string Title { get; }
        public string Info { get; }
        public long? Current { get; }
        public long? Total { get; }

        /// <summary>How far it is, from 0 to 1.</summary>
        public double? Fraction { get; }

        /// <summary>The progress in <paramref name="snapshot"/>, or null when it holds no report.</summary>
        internal static CommandProgress From(CliProgressState.Snapshot snapshot)
        {
            return snapshot.HasReport
                ? new CommandProgress(snapshot.Title, snapshot.Info, snapshot.Current, snapshot.Total, snapshot.Progress01)
                : null;
        }
    }
}
