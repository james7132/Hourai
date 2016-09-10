using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace DrumBot {

    [Module("module", AutoLoad = false)]
    [PublicOnly]
    [Permission(GuildPermission.ManageGuild, Require.User)]
    public class Module {

        static readonly Type HideType = typeof(HideAttribute);
        IEnumerable<string> Modules => Bot.CommandService.Modules.Where(m => !m.Source.IsDefined(HideType, false)).Select(m => m.Name).ToList();

        [Command]
        [Remarks("Lists all modules available. Enabled ones are highligted. Requires user to have ``Manage Server`` permission.")]
        public async Task ModuleList(IUserMessage message) {
            var config = Config.GetGuildConfig(Check.InGuild(message));
            await message.Respond(Modules.Select(s => (config.IsModuleEnabled(s)) ? s.Bold().Italicize() : s).Join(", "));
        }

        [Command("enable")]
        [Remarks("Enables a module for this server. Requires user to have ``Manage Server`` permission.")]
        public async Task ModuleEnable(IUserMessage message, params string[] modules) {
            var response = new StringBuilder();
            var config = Config.GetGuildConfig(Check.InGuild(message));
            foreach (string module in modules) {
                if(Modules.Contains(module, StringComparer.OrdinalIgnoreCase)) {
                    config.AddModule(module);
                    response.AppendLine($"{Config.SuccessResponse}: Module {module} enabled.");
                } else {
                    response.AppendLine($"No module named {module} found.");
                }
            }
            await message.Respond(response.ToString());
        }

        [Command("disable")]
        [Remarks("Disable a module for this server. Requires user to have ``Manage Server`` permission.")]
        public async Task ModuleDisable(IUserMessage message, params string[] modules) {
            var response = new StringBuilder();
            var config = Config.GetGuildConfig(Check.InGuild(message));
            foreach (string module in modules) {
                if (Modules.Contains(module, StringComparer.OrdinalIgnoreCase)) {
                    config.RemoveModule(module);
                    response.AppendLine($"{Config.SuccessResponse}: Module {module} disabled.");
                } else {
                    response.AppendLine($"No module named {module} found.");
                }
            }
            await message.Respond(response.ToString());
        }
    }
}
