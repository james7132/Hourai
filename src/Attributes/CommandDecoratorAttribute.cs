using System;
using System.Threading.Tasks;
using Discord.Commands;

namespace DrumBot {

    [AttributeUsage(AttributeTargets.Method)]
    public abstract class CommandBuilderAttribte : Attribute {

        public abstract CommandBuilder Build(string name, CommandBuilder builder);

    }

    [AttributeUsage(AttributeTargets.Method)]
    public abstract class CommandDecoratorAttribute : Attribute {

        public abstract Func<CommandEventArgs, Task> Decorate(string name, Func<CommandEventArgs, Task> task);

    }
}
