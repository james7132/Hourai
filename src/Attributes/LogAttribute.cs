using System;
using System.Threading.Tasks;
using Discord.Commands;

namespace DrumBot {

    [AttributeUsage(AttributeTargets.Method)]
    public class LogAttribute : CommandDecoratorAttribute {
        public override Func<CommandEventArgs, Task> Decorate(string name, 
                                         Func<CommandEventArgs, Task> task) {
            return delegate(CommandEventArgs evt) {
                Log.Info($"Command { name } was triggered by { evt.User.Name } on { evt.Server.Name }");
                return task(evt);
            };
        }
    }
}
