using System;
using Discord.Commands;

namespace DrumBot {

    /// <summary>
    /// Attribute that adds a check of a certain type to a command.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class CheckAttribute : CommandBuilderAttribte {

        public Type Type { get; set; }

        public CheckAttribute(Type type) { Type = type; }

        /// <summary>
        /// Callback to process a command.
        /// </summary>
        /// <param name="name">the name of the command</param>
        /// <param name="builder">the ComamndBuilder for the current command</param>
        /// <returns>the CommandBuilder edited with the additioanl data</returns>
        public override CommandBuilder Build(string name, CommandBuilder builder) {
            Log.Info($"[{name}] Adding check \"{ Type.Name }\".");
            var check = Activator.CreateInstance(Type) as Checker;
            if (check == null) {
                Log.Info($"[{name}] ERROR: {Type.Name} does not inherit from Checker. Skipping.");
                return builder; 
            }
            check.Name = name;
            return builder.AddCheck(check);
        }
    }
}
