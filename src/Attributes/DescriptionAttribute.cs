using System;
using Discord.Commands;

namespace DrumBot.src.Attributes {

    [AttributeUsage(AttributeTargets.Method)]
    public class DescriptionAttribute : CommandBuilderAttribte {

        public string Description { get; set; }

        public DescriptionAttribute(string description) {
            Description = description;
        }

        public override CommandBuilder Build(string name, CommandBuilder builder) {
            if (string.IsNullOrEmpty(Description))
                return builder;
            Log.Info($"Adding description of command \"{name}\" to \"{Description}\"");
            return builder.Description(Description);
        }
    }
}
