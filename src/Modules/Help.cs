using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Command = Discord.Commands.Command;

namespace DrumBot {

    /// <summary>
    /// Generates a help method for all of a bot commands.
    /// Cannot be automatically installed and must be installed after all other modules have been installed.
    /// </summary>
    [Module(AutoLoad = false)]
    public class Help {
        readonly Dictionary<Module, CommandGroup> Modules;

        static Queue<string> SplitComamnd(string input) {
            return new Queue<string>(Regex.Replace(input.Trim(), @"\s\s+", " ").Split(' '));
        }

        public class CommandGroup {

            public string Name{  get; }
            public Command Command { get; }
            public Dictionary<string, CommandGroup> Subcommands { get; }

            CommandGroup() {
                Subcommands = new Dictionary<string, CommandGroup>();
            }

            public CommandGroup(IEnumerable<Command> commands) : this() {
                Name = "";
                Command = null;
                foreach (Command command in commands.EmptyIfNull())
                    AddCommand(command);
            }

            CommandGroup(string name) : this() {
                Name = name;
            }

            CommandGroup(Command command, string name) : this(name) {
                Name = name;
                Command = command;
            }

            void AddCommand(Command command) {
                AddCommand(SplitComamnd(command.Text), command);
            }

            void AddCommand(Queue<string> names, Command command) {
                Check.NotNull(names);
                if (names.Count == 0)
                    return;
                var name = names.Dequeue();
                if (names.Count == 0) {
                    if (Subcommands.ContainsKey(name))
                        throw new Exception("Cannot register two commands to the same name");
                    Subcommands.Add(name, new CommandGroup(command, name));
                } else {
                    if(!Subcommands.ContainsKey(name))
                        Subcommands[name] = new CommandGroup(name);
                    Subcommands[name].AddCommand(names, command);
                }
            }

            public Task<string> GetSpecficHelp(IMessage message, string command) {
                return GetSpecficHelp(message, SplitComamnd(command));
            }

            Task<string> GetSpecficHelp(IMessage message, Queue<string> names) {
                if (names.Count == 0)
                    return GetSpecficHelp(message);
                var prefix = names.Dequeue();
                // Recurse as needed
                if (Subcommands.ContainsKey(prefix))
                    return Subcommands[prefix].GetSpecficHelp(message, names);
                return null;
            }

            async Task<string> GetSpecficHelp(IMessage message) {
                var builder = new StringBuilder();
                Command c = Command;
                using (builder.Code()) {
                    builder.Append(Name);
                    if(c != null) {
                        foreach (CommandParameter parameter in c.Parameters) {
                            var name = parameter.Name;
                            if(parameter.IsMultiple || parameter.IsRemainder)
                                name += "...";
                            if (parameter.IsOptional)
                                name = name.SquareBracket();
                            if(!name.IsNullOrEmpty())
                                builder.Append(" " + name);
                        }
                    }
                }
                if (c != null)
                    builder.AppendLine(c.Description);
                if(Subcommands.Count > 0) {
                    builder.AppendLine("Available Subcommands:");
                    builder.AppendLine(await ListSubcommands(message));
                }
                return builder.ToString();
            }

            public async Task<string> ListSubcommands(IMessage message) {
                var usable = new List<string>();
                foreach (var commandGroup in Subcommands.Values) {
                    if(await commandGroup.CanUse(message))
                        usable.Add(commandGroup.ToString().Code());
                }
                return usable.Join(", ");
            }

            public async Task<bool> CanUse(IMessage message) {
                if (Command != null) {
                    var result = await Command.CheckPreconditions(message);
                    if (result.IsSuccess)
                        return true;
                }
                foreach (CommandGroup commandGroup in Subcommands.Values) {
                    if (await commandGroup.CanUse(message))
                        return true;
                }
                return false;
            }

            public override string ToString() {
                var str = Name.ToLowerInvariant();
                if (Subcommands.Count > 0)
                    str += "*";
                return str;
            }
        }

        public Help() {
            var commands = Bot.CommandService?.Commands;
            if(commands == null)
                throw new InvalidOperationException("Cannot create a help command if there is no command service");
            Modules = commands.GroupBy(c => c.Module)
                            .ToDictionary(g => g.Key, g => new CommandGroup(g));
        }

        [Command("help")]
        [Description("Gets information about commands")]
        public async Task HelpCommand(IMessage message, [Remainder] string command = "") {
            if(command.IsNullOrEmpty()) {
                await message.Respond(await GetGeneralHelp(message));
                return;
            }
            foreach (CommandGroup commandGroup in Modules.Values) {
                Log.Info(commandGroup);
                string specificHelp = await commandGroup.GetSpecficHelp(message, command);
                if (specificHelp.IsNullOrEmpty())
                    continue;
                await message.Respond(specificHelp);
                return;
            }
            await message.Respond($"{message.Author.Mention}: No command named {command.DoubleQuote()} found");
        }

        async Task<string> GetGeneralHelp(IMessage message) {
            var builder = new StringBuilder();
            foreach (var kvp in Modules) {
                if (!await kvp.Value.CanUse(message))
                    continue;
                var name = kvp.Key.Name;
                if (!name.IsNullOrEmpty())
                    builder.Append((name + ": ").Bold());
                builder.AppendLine(await kvp.Value.ListSubcommands(message));
            }
            return $"{message.Author.Mention} here are the commands you can use:\n{builder}\nRun ``help <command>`` for more information";
        }

    }
}
