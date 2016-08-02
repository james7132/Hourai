using System;
using Discord.Commands;

namespace DrumBot {

    /// <summary>
    /// Attribute that adds a parameter to a command
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class ParameterAttribute : CommandBuilderAttribte {

        public string Parameter { get; set; }
        public ParameterType Type { get; set; }

        public ParameterAttribute(string parameter, ParameterType type = ParameterType.Required) {
            Parameter = parameter;
            Type = type;
        }

        /// <summary>
        /// Callback to process a command.
        /// </summary>
        /// <param name="name">the name of the command</param>
        /// <param name="builder">the ComamndBuilder for the current command</param>
        /// <returns>the CommandBuilder edited with the additioanl data</returns>
        public override CommandBuilder Build(string name, CommandBuilder builder) {
            if (string.IsNullOrEmpty(Parameter))
                return builder;
            Log.Info($"[{name}] Adding parameter: \"{ Parameter }\"");
            return builder.Parameter(Parameter, Type);
        }
    }
}
