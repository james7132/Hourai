using System;
using Discord.Commands;

namespace DrumBot.src.Attributes {

    [AttributeUsage(AttributeTargets.Method)]
    public class AliasAttribute : CommandBuilderAttribte {
        public string[] Aliases { get; set; }

        public AliasAttribute(params string[] aliases) { Aliases = aliases; }

        public override CommandBuilder Build(string name, CommandBuilder builder) {
            if (Aliases.Length <= 0)
                return builder;
            Log.Info($"Adding alia{ (Aliases.Length > 1 ? "es" : "s") } \"{ string.Join(", ", Aliases) }\" to command \"{ name }\"");
            return builder.Alias(Aliases);
        }
    }
}
