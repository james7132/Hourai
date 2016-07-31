using System;
using System.Threading.Tasks;
using Discord.Commands;

namespace DrumBot.src.Attributes {

    [AttributeUsage(AttributeTargets.Method)]
    public class CheckPermissionsAttribute : CommandDecoratorAttribute {
        public override Func<CommandEventArgs, Task> Decorate(string name, Func<CommandEventArgs, Task> task) {  
            return delegate(CommandEventArgs args) {
                if (DrumBot.Config.GetServerConfig(args.Server).AllowCommands) {
                    return task(args);
                }
                Log.Info($"Command \"{ name }\" cannot be used in { args.Server.Name } as it is not a PROD server.");
                return Task.FromResult(0);
            };
        }
    }
}
