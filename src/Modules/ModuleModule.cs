using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Commands.Permissions.Visibility;
using Discord.Modules;

namespace DrumBot.src.Services {

    public class ModuleModule : IModule {
        const string Permission = "Manage Server";
        const string ModuleParam = "Module Name";
        public void Install(ModuleManager manager) {
            var moduleService = manager.Client.GetService<ModuleService>();
            manager.CreateCommands("module", cbg => {
                cbg.PublicOnly()
                   .AddCheck(Check.ManageServer(bot: false));
                cbg.CreateCommand()
                    .Description("Lists all modules available. Enabled ones are highligted" + Utility.Requires(Permission))
                    .Do(Command.Response(e =>
                           moduleService.Modules.Where(m => m != manager)
                                .Select(m => m.EnabledServers.Contains(e.Server) 
                                    ? m.Name.Bold().Italicize()
                                    : m.Name)
                                .Join(", ")));
                cbg.CreateCommand("enable")
                  .Description("Enables a module for this server. " + Utility.Requires(Permission))
                  .Parameter(ModuleParam)
                  .Do(ModuleCommand("enabled", (m, s) => m.EnableServer(s), moduleService));
                cbg.CreateCommand("disable")
                  .Description("Disables a module for this server. " + Utility.Requires(Permission))
                  .Parameter(ModuleParam)
                  .Do(ModuleCommand("disabled", (m, s) => m.DisableServer(s), moduleService));
            });
        }

        Func<CommandEventArgs, Task> ModuleCommand(string action,
                                                   Action<ModuleManager, Server> func,
                                                   ModuleService service) {
            return Command.Response(delegate(CommandEventArgs e) {
                var module = GetModule(e, service);
                module.DisableServer(e.Server);
                return Success(module, "disabled");
            });
        }

        ModuleManager GetModule(CommandEventArgs e, ModuleService service) {
              var moduleName = e.GetArg(ModuleParam).ToLowerInvariant();
              var module = service.Modules.FirstOrDefault(m => m.Id== moduleName);
              if(module == null)
                  throw new NotFoundException("module", moduleName);
            return module;
        }

        string Success(ModuleManager module, string action) => $"{Config.SuccessResponse}: Module {module.Name.Bold()} {action}.";
    }
}
