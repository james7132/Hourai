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
                var names = SplitComamnd(command.Text);
                AddCommand(names, command);
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

            public string GetSpecficHelp(IMessage message, string command) {
                return GetSpecficHelp(message, command, SplitComamnd(command));
            }

            string GetSpecficHelp(IMessage message, string command, Queue<string> names) {
                if (names.Count == 0)
                    return null;
                var prefix = names.Dequeue();
                if(names.Count > 0) {
                    if (!Groups.ContainsKey(prefix))
                        return null;
                    return Groups[prefix].GetSpecficHelp(message, command, names);
                }
                var builder = new StringBuilder();
                Command c = null;
                if (Commands.ContainsKey(prefix)) {
                    c = Commands[prefix];
                }
                using (builder.Code()) {
                    builder.Append(command);
                    if(c != null) {
                        foreach (CommandParameter parameter in c.Parameters) {
                            var name = " " + parameter.Name;
                            if(parameter.IsMultiple || parameter.IsRemainder)
                                name += "...";
                            if (parameter.IsOptional)
                                name = name.SquareBracket();
                            builder.Append(name);
                        }
                    }
                }
                if (c != null) {
                    builder.AppendLine(c.Description);
                    foreach (var parameter in c.Parameters) {
                        if(!parameter.Description.IsNullOrEmpty())
                            builder.AppendLine($"{parameter.Name}: {parameter.Description}");
                    }
                }
                if(Groups.ContainsKey(prefix)) {
                    var g = Groups[prefix];
                    builder.AppendLine("Available Subcommands:");
                    builder.AppendLine(g.List(message));
                }
                return builder.ToString();
            }

            public string List(IMessage message) {
                var usable = new List<string>();
                usable.AddRange(Commands.Where(c => true).Select(c => c.Key));
                usable.AddRange(Groups.Where(c => c.Value.CanUse(message)).Select(c => c.Key + "*"));
                return usable.Select(s => s.Code()).Join(", ");
            }

            public bool CanUse(IMessage user) {
                return Commands.Any(c => true) || Groups.Any(g => true); // TODO: Permission check
            }
        }

        public Help() {
            var commands = Bot.CommandService?.Commands;
            if(commands == null)
                throw new InvalidOperationException("Cannot create a help command if there is no command service");
            Modules = commands.ToKVStream(c => c.Module, c => c)
                            .GroupByKey()
                            .MapValue(v => new CommandGroup(v))
                            .Evaluate();
        }

        [Command("help")]
        [Description("Gets information about commands")]
        public async Task HelpCommand(IMessage message, 
                [Remainder, Description("The command to get information on")] string command = "") {
            if(command.IsNullOrEmpty()) {
                await message.Respond(GetGeneralHelp(message));
            } else {
                foreach (var module in Modules) {
                    var help = module.Value.GetSpecficHelp(message, command);
                    if (help.IsNullOrEmpty())
                        continue;
                    await message.Respond(help);
                    return;
                }
                await message.Respond($"{message.Author.Mention}: No command named {command.DoubleQuote()} found");
            }
        }

        string GetGeneralHelp(IMessage message) {
            var modules = Modules.Where(m => m.Value.CanUse(message))
                                    .Select(m => $"{m.Key.Name.Bold()}{(m.Key.Name.IsNullOrEmpty() ? "" : ":")} {m.Value.List(message)}")
                                    .Join("\n");
            return $"{message.Author.Mention} here are the commands you can use:\n{modules}\n\nRun ``help <command>`` for more information";
        }

    }
}
