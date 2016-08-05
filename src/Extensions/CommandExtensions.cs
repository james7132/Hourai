using System;
using Discord.Commands;
using Discord.Modules;

namespace DrumBot {
    public static class CommandExtensions {

        public static CommandGroupBuilder CreateGroup(
            this CommandGroupBuilder builder,
            Action<CommandGroupBuilder> buildFunc = null) {
            return builder.CreateGroup(string.Empty, buildFunc);
        }

        public static void CreateCommands(
            this ModuleManager manager,
            Action<CommandGroupBuilder> buildFunc = null) {
            manager.CreateCommands(string.Empty, buildFunc);
        }

    }
}
