using System;
using Discord.Commands;

namespace DrumBot {

    [AttributeUsage(AttributeTargets.Method)]
    public class DescriptionAttribute : CommandBuilderAttribte {

        public string Description { get; set; }

        public DescriptionAttribute(string description) {
            Description = description;
        }

        public override CommandBuilder Build(string name, CommandBuilder builder) {
            if (string.IsNullOrEmpty(Description))
                return builder;
            Log.Info($"[{name}] Setting description to \"{Description}\"");
            return builder.Description(Description);
        }
    }
}
