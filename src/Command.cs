using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace DrumBot {
    public class Command {

        public static async Task ForEveryUser(CommandEventArgs e, IEnumerable<User> users, Func<User, Task<string>> func) {
            string[] results = await Task.WhenAll(e.Message.MentionedUsers.Select(func));
            string response;
            if (results.Length > 0)
                response = string.Join("\n", results);
            else 
                response ="No users specified. Please specify at least one target user.";
            await e.Respond(response);
        }

        public static Func<User, Task<string>> AdminAction(Channel channel,
                                                            string action,
                                                            Func<User, Task> task,
                                                            bool ignoreErrors = false) {
            var botUser = channel.Server.CurrentUser;
            return async delegate(User user) {
                if (user.IsServerOwner())
                    return $"{user.Name}: User is server's owner. Cannot { action }.";
                if(botUser.CompareTo(user) <= 0)
                    return $"{user.Name}: User has higher roles than {botUser.Name}. Cannot {action}.";
                string result = string.Empty;
                try {
                    await task(user);
                } catch (Exception exception) {
                    result = exception.Message;
                } 
                if (string.IsNullOrEmpty(result) || ignoreErrors)
                    return $"{user.Name}: { Bot.Config.SuccessResponse }";
                return $"{user.Name}: {result}";
            };
        }
    }
}
