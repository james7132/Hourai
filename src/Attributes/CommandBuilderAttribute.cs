using System;
using Discord.Commands;

namespace DrumBot {

    [AttributeUsage(AttributeTargets.Method)]
    public abstract class CommandBuilderAttribte : Attribute {

        public abstract CommandBuilder Build(string name, CommandBuilder builder);

    }
}
