using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace DrumBot {
    public class Command {

        public static async Task ForEvery<T>(CommandEventArgs e, IEnumerable<T> users, Func<T, Task<string>> func) {
            string[] results = await Task.WhenAll(users.Select(func));
            string response;
            if (results.Length > 0)
                response = string.Join("\n", results);
            else 
                response ="No users specified. Please specify at least one target user.";
            await e.Respond(response);
        }

        public static Func<User, Task<string>> Action(Channel channel,
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
                    return $"{user.Name}: { Config.SuccessResponse }";
                return $"{user.Name}: {result}";
            };
        }

        public static Func<Channel, Task<string>> Action(Channel channel,
                                                                Func<Channel, Task> task,
                                                                bool ignoreErrors = false) {
            return async delegate(Channel targetChannel) {
                string result = string.Empty;
                try {
                    await task(targetChannel);
                } catch (Exception exception) {
                    result = exception.Message;
                } 
                if (string.IsNullOrEmpty(result) || ignoreErrors)
                    return $"{targetChannel.Name}: { Config.SuccessResponse }";
                return $"{targetChannel.Name}: {result}";
            };
        }
    }
}
