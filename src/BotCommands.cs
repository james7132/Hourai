using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace DrumBot {
    public class BotCommands {

        static readonly Func<User, Role, Task> AddRoleFunc =
            async (user, role) => await user.AddRoles(role);
        static readonly Func<User, Role, Task> RemoveRoleFunc =
            async (user, role) => await user.RemoveRoles(role);

        [Command]
        [Alias("find")]
        [Description("Search the history of the current channel for a certain value.")]
        [Parameter("SearchTerm")]
        [Check(typeof(LogChecker))]
        [Check(typeof(ProdChecker))]
        public static async void Search(CommandEventArgs e) {
            string reply = await Bot.ChannelSet.Get(e.Channel).Search(e.GetArg("SearchTerm"));
            await e.Channel.Respond($"{e.User.Mention}: Matches found in {e.Channel.Mention}:\n{reply}");
        }

        [Command]
        [Check(typeof(LogChecker))]
        static async void ServerConfig(CommandEventArgs e) {
            await e.Channel.Respond(
                    $"{e.User.Mention}, here is the server config for {e.Server.Name}:\n{Bot.Config.GetServerConfig(e.Server)}");
        }

        [Command]
        [Description("Adds a role to all mentioned users. Requires ``Manage Role`` permission for u1 and bot.")]
        [Parameter("Role")]
        [Parameter("Users", ParameterType.Multiple)]
        [Check(typeof(LogChecker))]
        [Check(typeof(ManageRolesChecker))]
        public static void AddRole(CommandEventArgs e) {
            Log.Info("ADDROLE");
            RoleCommand(e, e.Message.MentionedUsers, AddRoleFunc, "add");
        }

        [Command]
        [Description("Removes a role to all mentioned users. Requires ``Manage Role`` permission for u1 and bot.")]
        [Parameter("Role")]
        [Parameter("Users", ParameterType.Multiple)]
        [Check(typeof(LogChecker))]
        [Check(typeof(ManageRolesChecker))]
        public static void RemoveRole(CommandEventArgs e) {
            Log.Info("REMOVEROLE");
            RoleCommand(e, e.Message.MentionedUsers, RemoveRoleFunc, "remove");
        }

        [Command]
        [Description("Kicks all mentioned users. Requires ``Kick Members`` permission for u1 and bot")]
        [Parameter("Users", ParameterType.Multiple)]
        [Check(typeof(LogChecker))]
        [Check(typeof(KickMembersChecker))]
        public static async void Kick(CommandEventArgs e) {
            await e.Channel.Respond(await AdminAction(e, e.Message.MentionedUsers, "kick",
                async delegate (User user, Channel channel) {
                    await user.Kick();
                    return string.Empty;
                }));
        }

        [Command]
        [Description("Bans all mentioned users. Requires ``Ban Members`` permission for u1 and bot")]
        [Parameter("Users", ParameterType.Multiple)]
        [Check(typeof(LogChecker))]
        [Check(typeof(BanMembersChecker))]
        public static async void Ban(CommandEventArgs e) {
            await e.Channel.Respond(await AdminAction(e, e.Message.MentionedUsers, "ban",
                async delegate (User user, Channel channel) {
                    await user.Server.Ban(user);
                    return string.Empty;
                }));
        }

        [Command]
        [Description("Gets the avatar url of all mentioned users.")]
        [Parameter("Users", ParameterType.Multiple)]
        [Check(typeof(LogChecker))]
        public static async void Avatar(CommandEventArgs e) {
            await e.Channel.Respond(AllMentionedUsers(e, user => user.AvatarUrl));
        }

        [Command]
        [Description("Nukes all of a certain role from all members of the server")]
        [Parameter("Role")]
        [Check(typeof(ManageRolesChecker))]
        [Check(typeof(LogChecker))]
        public static void NukeRole(CommandEventArgs e) {
            RoleCommand(e, e.Server.Users, RemoveRoleFunc, "remove");
        }

        static string AllMentionedUsers(CommandEventArgs e,
                                    Func<User, string> task) {
            return string.Join("\n",
                e.Message.MentionedUsers.Select(u => $"{u.Mention}: {task(u)}"));
        }

        static int Compare(User u1, User u2) {
            Func<Role, int> rolePos = role => role.Position;
            return u1.Roles.Max(rolePos).CompareTo(u2.Roles.Max(rolePos));
        }

        static async Task<string> AdminAction(CommandEventArgs e,
                                        IEnumerable<User> users,
                                        string action,
                                        Func<User, Channel, Task<string>> task) {
            if(users == null)
                throw new ArgumentNullException();
            var builder = new StringBuilder();
            var botUser = e.Server.GetUser(Bot.Client.CurrentUser.Id);
            Func<User, Channel, Task> wrappedTask = async delegate(User user, Channel channel) {
                if (user == user.Server.Owner)
                    builder.AppendLine($"{user.Name}: User is server's owner. Cannot { action }.");
                else if(Compare(botUser, user) <= 0)
                    builder.AppendLine($"{user.Name}: User has higher roles than { botUser.Name }. Cannot { action }.");
                else {
                    string result = await task(user, channel);
                    builder.AppendLine(string.IsNullOrEmpty(result)
                        ? $"{user.Name}: :thumbsup:"
                        : $"{user.Name}: {result}");
                }
            };
            if (!users.Any())
                builder.AppendLine("No users specified. Please specify at least one target user.");
            else
                await Task.WhenAll(users.Select(user => wrappedTask(user, e.Channel)));
            return builder.ToString();
        }

        static Role GetRole(string roleName, User caller,  string action, out string error) {
            var server = caller.Server;
            Role role = server.FindRoles(roleName).FirstOrDefault();
            if (role == null)
                error = $"No role named {roleName} found.";
            else {
                error = RoleCheck(server.GetUser(Bot.Client.CurrentUser.Id),
                    role, action) ?? RoleCheck(caller, role, action);
            }
            return role;
        }

        static string RoleCheck(User user, Role role, string action) {
            int position = role.Position;
            if (user.Server.Owner != user && user.Roles.Max(r => r.Position) <= position)
                return $"{user.Name} cannot {action} role \"{role.Name}\", as it is above their role.";
            return null;
        }

        static async void RoleCommand(CommandEventArgs e, IEnumerable<User> users, Func<User, Role, Task> task, string action) {
            string response;
            Role role = GetRole(e.GetArg("Role"), e.User, action, out response);
            await e.Channel.Respond(response ?? await AdminAction(e, users, action + " role",
                async delegate(User user, Channel channel) {
                    await task(user, role);
                    return string.Empty;
                }));
        }
    }
}
