using System.Linq;
using Discord.Commands;
using Discord.Commands.Permissions.Visibility;
using Discord.Modules;

namespace DrumBot.src.Services {

    public class ModuleModule : IModule {
        const string requires = "Requires ``Manage Server`` permission on user.";
        public void Install(ModuleManager manager) {
            var moduleService = manager.Client.GetService<ModuleService>();
            const string moduleParam = "Module Name";
            manager.CreateCommands("module", cbg => {
                cbg.AddCheck(new ProdChecker());
                cbg.AddCheck(Check.ManageServer(bot: false));
                cbg.CreateCommand()
                   .PublicOnly()
                   .Description("Lists all modules available. " + requires)
                   .Do(async delegate(CommandEventArgs e) {
                           await e.Respond(string.Join(", ", moduleService.Modules
                               .Where(m => m != manager)
                               .Select(m => m.EnabledServers.Contains(e.Server)
                                   ? m.Name.Bold().Italicize()
                                   : m.Name)));
                       });
                cbg.CreateCommand("enable")
                  .PublicOnly()
                  .Description("Enables a module for this server. " + requires)
                  .Parameter(moduleParam)
                  .Do(async e => {
                      var moduleName = e.GetArg(moduleParam).ToLowerInvariant();
                      var module = moduleService.Modules.FirstOrDefault(m => m.Id == moduleName);
                      if(module == null)
                          throw new NotFoundException("module", moduleName);
                      module.EnableServer(e.Server);
                      await e.Respond($"{Config.SuccessResponse}: Module {module.Name.Bold()} enabled.");
                  });
                cbg.CreateCommand("disable")
                  .PublicOnly()
                  .Description("Disables a module for this server. " + requires)
                  .Parameter(moduleParam)
                  .Do(async e => {
                      var moduleName = e.GetArg(moduleParam).ToLowerInvariant();
                      var module = moduleService.Modules.FirstOrDefault(m => m.Id== moduleName);
                      if(module == null)
                          throw new NotFoundException("module", moduleName);
                      module.DisableServer(e.Server);
                      await e.Respond($"{Config.SuccessResponse}: Module {module.Name.Bold()} disabled.");
                  });
            });
        }
    }
}
