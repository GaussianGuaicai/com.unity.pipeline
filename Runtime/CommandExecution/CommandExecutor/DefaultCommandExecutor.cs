using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Unity.Pipeline.Commands
{
    /// <summary>
    /// <see cref="ICommandExecutor"/> for one server: finds the request's command in the registry,
    /// binds its arguments, refuses it when it can't run now, and hands the rest to the queue. It
    /// refuses nothing itself, which is all a Player needs; the Editor's subclass adds its rules.
    /// </summary>
    class DefaultCommandExecutor : ICommandExecutor
    {
        readonly ICommandRegistry m_Registry;
        readonly ICommandQueue m_Queue;

        public DefaultCommandExecutor(ICommandRegistry registry, ICommandQueue queue)
        {
            m_Registry = registry ?? throw new ArgumentNullException(nameof(registry));
            m_Queue = queue ?? throw new ArgumentNullException(nameof(queue));
        }

        /// <inheritdoc/>
        public CommandExecution Execute(CommandRequest request)
        {
            var command = Bind(request, out var parameters);
            Refuse(command);

            var converted = Convert(command, parameters);
            return m_Queue.Enqueue(command, ToValues(parameters), converted, request);
        }

        /// <summary>
        /// Finds the request's command and binds its arguments: a command line's tokens, or the
        /// structured request's JSON object as it is. Takes the executor's own options out first:
        /// the queue reads them when it writes what the command returns.
        /// </summary>
        CommandInfo Bind(CommandRequest request, out JObject parameters)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var arguments = CommandRequest.ReadOptions(request.Arguments, out _, out _);
            var index = m_Registry.Index;
            if (request.ParametersJson != null)
            {
                if (arguments.Count > 0)
                    throw new ArgumentException(
                        $"A request by parameter name takes no command-line arguments, only {CommandRequest.VerboseOption} and {CommandRequest.OmitNullsOption}; got '{arguments[0]}'.");

                if (!index.TryFindCommand(request.Command, out var named))
                    throw BasePipelineServer.CommandNotFound(new BasePipelineServer.RawCommandMiss(request.Command, index.Commands, index.CommandsNamed(request.Command)));
                parameters = ParseParameters(request.ParametersJson);
                return named;
            }

            var tokens = new List<string>(arguments.Count + 1) { request.Command };
            tokens.AddRange(arguments);

            var resolution = BasePipelineServer.ResolveRawCommand(tokens, index);
            if (resolution.ListedGroup != null)
                throw new ArgumentException(
                    $"'{resolution.ListedGroup}' is a group, not a command. Name a command in it, as '{resolution.ListedGroup} <name>'.");

            var command = resolution.Command;
            var rest = tokens.GetRange(resolution.ArgumentsStart, tokens.Count - resolution.ArgumentsStart);
            if (!CommandLineBinder.TryBind(command, rest, out parameters, out var problems))
                throw new CommandArgumentsException(command, problems, BasePipelineServer.DescribeArgProblems(command, problems));

            // Binding succeeds when a required parameter simply wasn't given: that rule belongs to
            // parameter validation, and is checked here so an invalid request is refused before
            // it can be queued as a job. The refusal names the command the tokens resolved to.
            var missingRequired = BasePipelineServer.ValidateRequiredParametersBound(command, parameters);
            if (!string.IsNullOrEmpty(missingRequired))
                throw new ParameterValidationException(command.FullName, missingRequired);

            return command;
        }

        /// <summary>
        /// Reads a structured request's arguments as the HTTP transport received them: dates parse
        /// to dates, as they did when the transport handed the object over itself.
        /// </summary>
        static JObject ParseParameters(string json)
        {
            if (string.IsNullOrEmpty(json))
                return new JObject();

            var token = JToken.Parse(json);
            return token as JObject ?? throw new ArgumentException("The arguments must be a JSON object, by parameter name.");
        }

        /// <summary>
        /// Converts the bound arguments to the command's parameters' types and checks the required
        /// ones are there.
        /// </summary>
        static object[] Convert(CommandInfo command, JObject parameters)
        {
            var converted = BasePipelineServer.ExtractCommandParameters(command, parameters, out var conversionError);

            // Reject unconvertible arguments rather than silently running with the default.
            // The log keeps the name of the method this lived in, which clients and tests match.
            if (!string.IsNullOrEmpty(conversionError))
            {
                Debug.LogError($"ExecuteCommandByName: Parameter conversion failed: {conversionError}");
                throw new ParameterValidationException(command.FullName, conversionError);
            }

            var validationError = BasePipelineServer.ValidateCommandParameters(command, converted);
            if (!string.IsNullOrEmpty(validationError))
            {
                Debug.LogError($"ExecuteCommandByName: Parameter validation failed: {validationError}");
                throw new ParameterValidationException(command.FullName, validationError);
            }

            return converted;
        }


        /// <summary>
        /// The bound arguments by parameter name, as plain values: each a string, boolean, number,
        /// date or null, or a list or dictionary of them for a structured value.
        /// </summary>
        static IReadOnlyDictionary<string, object> ToValues(JObject parameters)
        {
            var values = new Dictionary<string, object>(parameters.Count);
            foreach (var property in parameters.Properties())
                values[property.Name] = ToValue(property.Value);
            return values;
        }

        static object ToValue(JToken token)
        {
            switch (token)
            {
                case JObject obj:
                    return ToValues(obj);
                case JArray array:
                    var items = new List<object>(array.Count);
                    foreach (var item in array)
                        items.Add(ToValue(item));
                    return items;
                case JValue value:
                    return value.Value;
                default:
                    return token.ToString();
            }
        }

        /// <summary>
        /// Throws <see cref="CommandUnavailableException"/> when <paramref name="command"/> can't run
        /// now. Called on the caller's thread, so an override reads only thread-safe state. Refuses
        /// nothing by default.
        /// </summary>
        protected virtual void Refuse(CommandInfo command) { }

        /// <summary>
        /// Told about each command that ran, once it ends, synchronous or a detached job; a
        /// command refused isn't reported. Called on whatever thread
        /// the command ended on, so an override sticks to thread-safe work or moves to the main
        /// thread itself, and never throws: the command's caller would see the failure.
        /// </summary>
        /// <param name="info">The execution that ended. It carries no transaction.</param>
        protected virtual void OnCommandEnded(in CommandExecutionInfo info) { }

        /// <summary>Reports a command of this executor's queue that ran, once it ends.</summary>
        internal void ReportEnded(in CommandExecutionInfo info) => OnCommandEnded(info);
    }
}
