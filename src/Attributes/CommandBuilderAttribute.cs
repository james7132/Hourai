using System;
using Discord.Commands;

namespace DrumBot {

    /// <summary>
    /// Abstract attribute class for designating attributes that affect the construction of a command
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public abstract class CommandBuilderAttribte : Attribute {

        /// <summary>
        /// Callback to process a command.
        /// </summary>
        /// <param name="name">the name of the command</param>
        /// <param name="builder">the ComamndBuilder for the current command</param>
        /// <returns>the CommandBuilder edited with the additioanl data</returns>
        public abstract CommandBuilder Build(string name, CommandBuilder builder);

    }
}
