using System;
using Discord.Commands;

namespace DrumBot {

    [AttributeUsage(AttributeTargets.Method)]
    public class AliasAttribute : CommandBuilderAttribte {
        public string[] Aliases { get; set; }

        public AliasAttribute(params string[] aliases) { Aliases = aliases; }

        public override CommandBuilder Build(string name, CommandBuilder builder) {
            if (Aliases.Length <= 0)
                return builder;
            Log.Info($"[{name}] Adding alias{ (Aliases.Length > 1 ? "es" : string.Empty) } \"{ string.Join(", ", Aliases) }\"");
            return builder.Alias(Aliases);
        }
    }
}
