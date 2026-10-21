namespace Unity.Pipeline.Commands
{
    /// <summary>
    /// A command's outcome, as the exit code the Pipeline protocol's exit frame carries. A command
    /// that ran ends Success, Failed, InvalidArguments, Cancelled or TimedOut; the other values are
    /// what a transport answers the executor's exceptions with. What the command produced goes to
    /// the request's Out, and why it failed to its Error, never into its result.
    /// </summary>
    enum CommandResult
    {
        Success = 0,
        Failed = 1,
        /// <summary>The arguments don't fit the command, as it found once it ran.</summary>
        InvalidArguments = 2,
        NotFound = 3,
        /// <summary>Can't run now: settling, a modal dialog, compiling, reloading, Safe Mode.</summary>
        Unavailable = 4,
        Cancelled = 5,
        /// <summary>Too many commands wait or run.</summary>
        TooManyCommands = 6,
        /// <summary>The request's timeout ran out, while it waited or while it ran.</summary>
        TimedOut = 7
    }
}
