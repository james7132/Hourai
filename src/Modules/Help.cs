using System;
using System.Collections.Generic;
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

            public string Prefix{  get; }
            public Dictionary<string, Command> Commands { get; }
            public Dictionary<string, CommandGroup> Groups { get; }

            CommandGroup() {
                Commands = new Dictionary<string, Command>();
                Groups = new Dictionary<string, CommandGroup>();
            }

            public CommandGroup(IEnumerable<Command> commands) : this() {
                Prefix = "";
                foreach (Command command in commands.EmptyIfNull())
                    AddCommand(command);
            }

            CommandGroup(string prefix) : this() {
                Prefix = prefix;
            }

            void AddCommand(Command command) {
                AddCommand(SplitComamnd(command.Text), command);
            }

            void AddCommand(Queue<string> names, Command command) {
                Check.NotNull(names);
                if (names.Count == 0)
                    return;
                var prefix = names.Dequeue();
                if (names.Count == 0) {
                    if (Commands.ContainsKey(prefix))
                        throw new Exception("Cannot register two commands to the same name");
                    Commands.Add(prefix, command);
                } else {
                    if(!Groups.ContainsKey(prefix))
                        Groups[prefix] = new CommandGroup(prefix);
                    Groups[prefix].AddCommand(names, command);
                }
            }

            public Task<string> GetSpecficHelp(IMessage message, string command) {
                return GetSpecficHelp(message, command, SplitComamnd(command));
            }

            async Task<string> GetSpecficHelp(IMessage message, string command, Queue<string> names) {
                if (names.Count == 0)
                    return null;
                var prefix = names.Dequeue();
                if(names.Count > 0) {
                    if (!Groups.ContainsKey(prefix))
                        return null;
                    return await Groups[prefix].GetSpecficHelp(message, command, names);
                }
                var builder = new StringBuilder();
                Command c = null;
                if (Commands.ContainsKey(prefix))
                    c = Commands[prefix];
                using (builder.Code()) {
                    builder.Append(command);
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
                if(Groups.ContainsKey(prefix)) {
                    var g = Groups[prefix];
                    builder.AppendLine("Available Subcommands:");
                    builder.AppendLine(await g.List(message));
                }
                return builder.ToString();
            }

            public async Task<string> List(IMessage message) {
                var usable = new List<string>();
                usable.AddRange(Commands.Where(c => true).Select(c => c.Key));
                foreach (var kvp in Groups) {
                    if(await kvp.Value.CanUse(message))
                        usable.Add(kvp.Key + "*");
                }
                return usable.Select(s => s.Code()).Join(", ");
            }

            public async Task<bool> CanUse(IMessage message) {
                foreach (Command command in Commands.Values) {
                    var result = await command.CheckPreconditions(message);
                    if (result.IsSuccess)
                        return true;
                }
                foreach (CommandGroup commandGroup in Groups.Values) {
                    if (await commandGroup.CanUse(message))
                        return true;
                }
                return false;
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
            if(command.IsNullOrEmpty())
                await message.Respond(await GetGeneralHelp(message));
            foreach (CommandGroup commandGroup in Modules.Values) {
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
                    builder.Append((name + ":").Bold());
                builder.AppendLine(await kvp.Value.List(message));
            }
            return $"{message.Author.Mention} here are the commands you can use:\n{builder}\n\nRun ``help <command>`` for more information";
        }

    }
}
