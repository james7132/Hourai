using System;
using Discord.Commands;
using Discord.Commands.Permissions;

namespace DrumBot {

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class CheckAttribute : CommandBuilderAttribte {

        public Type Type { get; set; }

        public CheckAttribute(Type type) { Type = type; }

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
