using System;
using Discord.Commands;

namespace DrumBot {

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class ParameterAttribute : CommandBuilderAttribte {

        public string Parameter { get; set; }
        public ParameterType Type { get; set; }

        public ParameterAttribute(string parameter, ParameterType type = ParameterType.Required) {
            Parameter = parameter;
            Type = type;
        }

        public override CommandBuilder Build(string name, CommandBuilder builder) {
            if (string.IsNullOrEmpty(Parameter))
                return builder;
            Log.Info($"[{name}] Adding parameter: \"{ Parameter }\"");
            return builder.Parameter(Parameter, Type);
        }
    }
}
