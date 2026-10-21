using System;

namespace Unity.Pipeline.Commands
{
    /// <summary>
    /// Marks a static method as a CLI command with an explicit group and name, invoked as
    /// <c>unity command &lt;group&gt; &lt;name&gt;</c>. Declared with only a name, the command is in
    /// the <c>global</c> group and is also invoked by its name alone (<c>unity command &lt;name&gt;</c>).
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class CliCommandAttribute : Attribute
    {
        /// <summary>
        /// Group the command belongs to, as <c>.</c>-separated segments (e.g. <c>animation.animator</c>),
        /// or <c>global</c> when the attribute names only the command.
        /// </summary>
        public string Group { get; }

        /// <summary>
        /// Name of the command within its <see cref="Group"/>.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Human-readable description of what the command does.
        /// Used in CLI help text and command discovery.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Whether this command requires Unity main thread execution.
        /// Default: true (most Unity APIs require main thread).
        /// </summary>
        public bool MainThreadRequired { get; set; } = true;

        /// <summary>
        /// Optional hierarchical tags used to browse commands across groups (e.g. "runtime" for the
        /// Player command surface). Path-style: a '/' separates tag from subtag (e.g. "assets/import").
        /// Default: empty (untagged).
        /// </summary>
        public string[] Tags { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Create a CLI command attribute for a command in <paramref name="group"/>.
        /// </summary>
        /// <param name="group">Group the command belongs to</param>
        /// <param name="name">Name of the command within its group</param>
        /// <param name="description">Human-readable description</param>
        /// <example>
        /// <para>A command invoked as <c>unity command lighting bake</c>:</para>
        /// <code>[CliCommand("lighting", "bake", "Bake every lightmap")]</code>
        /// </example>
        public CliCommandAttribute(string group, string name, string description)
        {
            Group = group ?? throw new ArgumentNullException(nameof(group));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Description = description ?? throw new ArgumentNullException(nameof(description));
        }

        /// <summary>
        /// Create a CLI command attribute for a command in the <c>global</c> group.
        /// </summary>
        /// <param name="name">Name of the command in the <c>global</c> group</param>
        /// <param name="description">Human-readable description</param>
        [Obsolete("Name the command's group: [CliCommand(group, name, description)]. With only a name the command is registered in the 'global' group.")]
        public CliCommandAttribute(string name, string description)
            : this("global", name, description)
        {
        }
    }
}
