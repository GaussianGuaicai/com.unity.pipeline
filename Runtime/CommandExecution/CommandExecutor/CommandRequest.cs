using System;
using System.Collections.Generic;
using System.IO;

namespace Unity.Pipeline.Commands
{
    /// <summary>
    /// One command a client asked to run, as a process is run: the command as the client named it
    /// and its arguments as argv tokens, and its three text streams. The command may be just the
    /// first of the command's names, the rest leading the arguments. Each time it runs is an
    /// execution of its own, with its own id, which is what cancels it.
    /// </summary>
    /// <remarks>
    /// Out takes what the command's run produced, Error why it failed. The writers are safe on any
    /// thread. Until commands write their own output, what a command returns is written to Out as
    /// JSON, shaped by the executor's own options in the arguments: <see cref="VerboseOption"/>
    /// and <see cref="OmitNullsOption"/>.
    /// </remarks>
    sealed class CommandRequest
    {
        /// <summary>
        /// The executor's option that keeps the metadata of a response nested in what the command
        /// returns: when it ran and how long it took, and its null message and errors.
        /// </summary>
        /// <remarks>
        /// Every command takes the executor's options, anywhere before <c>--</c>, and none binds
        /// them to a parameter of its own.
        /// </remarks>
        public const string VerboseOption = "--verbose";

        /// <summary>The executor's option that drops nulls from what the command returns.</summary>
        public const string OmitNullsOption = "--omit-nulls";

        /// <summary>
        /// Reads the executor's own options in <paramref name="arguments"/>, before any <c>--</c>,
        /// and returns the rest. A token starting with <c>--</c> is never an option's value, so one
        /// taken out was never a command's argument.
        /// </summary>
        public static List<string> ReadOptions(IReadOnlyList<string> arguments, out bool verbose, out bool omitNulls)
        {
            var rest = new List<string>(arguments.Count);
            verbose = false;
            omitNulls = false;
            var endOfOptions = false;
            foreach (var argument in arguments)
            {
                if (!endOfOptions && argument == VerboseOption)
                    verbose = true;
                else if (!endOfOptions && argument == OmitNullsOption)
                    omitNulls = true;
                else
                {
                    endOfOptions |= argument == "--";
                    rest.Add(argument);
                }
            }

            return rest;
        }

        /// <param name="command">The command as the client named it, or just its group.</param>
        /// <param name="arguments">The argv tokens after <paramref name="command"/>; null for none.</param>
        /// <param name="input">What the client sends the command; null for nothing.</param>
        /// <param name="output">Takes what the command's run produces; null to discard it.</param>
        /// <param name="error">Takes why the command failed; null to discard it.</param>
        public CommandRequest(string command, IReadOnlyList<string> arguments, TextReader input, TextWriter output,
            TextWriter error)
        {
            Command = command ?? throw new ArgumentNullException(nameof(command));
            Arguments = arguments ?? Array.Empty<string>();
            In = input ?? TextReader.Null;
            Out = TextWriter.Synchronized(output ?? TextWriter.Null);
            Error = TextWriter.Synchronized(error ?? TextWriter.Null);
        }

        /// <summary>The command as the client named it, or just its group when the arguments lead with the rest.</summary>
        public string Command { get; }

        /// <summary>
        /// The argv tokens after <see cref="Command"/>, the executor's own options among them. For a
        /// request with <see cref="ParametersJson"/>, only the executor's own options.
        /// </summary>
        public IReadOnlyList<string> Arguments { get; }

        /// <summary>What the client sends the command.</summary>
        public TextReader In { get; }

        /// <summary>Takes what the command's run produces.</summary>
        public TextWriter Out { get; }

        /// <summary>Takes why the command failed.</summary>
        public TextWriter Error { get; }

        /// <summary>How long the command may run, the wait for the commands before it included; null for its own default.</summary>
        public TimeSpan? Timeout { get; set; }

        /// <summary>Who sent the request, for the reports once it ends; its default when the client didn't say.</summary>
        public Requester Requester { get; set; }

        /// <summary>
        /// The arguments as a JSON object by parameter name, in place of <see cref="Arguments"/>,
        /// for the HTTP transport's structured requests. Null for an argv request.
        /// </summary>
        internal string ParametersJson { get; set; }
    }
}
