using System;
using Discord.Commands;

namespace DrumBot {

    /// <summary>
    /// Attribute that applies alias(es) to a command.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class AliasAttribute : CommandBuilderAttribte {
        public string[] Aliases { get; set; }

        public AliasAttribute(params string[] aliases) { Aliases = aliases; }

        /// <summary>
        /// Callback to process a command.
        /// </summary>
        /// <param name="name">the name of the command</param>
        /// <param name="builder">the ComamndBuilder for the current command</param>
        /// <returns>the CommandBuilder edited with the additioanl data</returns>
        public override CommandBuilder Build(string name, CommandBuilder builder) {
            if (Aliases.Length <= 0)
                return builder;
            Log.Info($"[{name}] Adding alias{ (Aliases.Length > 1 ? "es" : string.Empty) } \"{ string.Join(", ", Aliases) }\"");
            return builder.Alias(Aliases);
        }
    }
}
