using System;
using Discord.Commands;

namespace DrumBot {

    /// <summary>
    /// Attribute that adds a help description to the command that is being built.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class DescriptionAttribute : CommandBuilderAttribte {

        public string Description { get; set; }

        public DescriptionAttribute(string description) {
            Description = description;
        }

        /// <summary>
        /// Callback to process a command.
        /// </summary>
        /// <param name="name">the name of the command</param>
        /// <param name="builder">the ComamndBuilder for the current command</param>
        /// <returns>the CommandBuilder edited with the additioanl data</returns>
        public override CommandBuilder Build(string name, CommandBuilder builder) {
            if (string.IsNullOrEmpty(Description))
                return builder;
            Log.Info($"[{name}] Setting description to \"{Description}\"");
            return builder.Description(Description);
        }
    }
}
