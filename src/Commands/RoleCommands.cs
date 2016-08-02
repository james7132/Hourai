using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace DrumBot {
    static class RoleCommands {

        static readonly Func<User, Role, Task> AddRoleFunc =
            async (user, role) => await user.AddRoles(role);
        static readonly Func<User, Role, Task> RemoveRoleFunc =
            async (user, role) => await user.RemoveRoles(role);

        [Command]
        [Group("role")]
        [Description("Adds a role to all mentioned users. Requires ``Manage Role`` permission for u1 and bot.")]
        [Parameter("Role")]
        [Parameter("Users", ParameterType.Multiple)]
        [Check(typeof(ProdChecker))]
        [Check(typeof(ManageRolesChecker))]
        public static void Add(CommandEventArgs e) {
            RoleCommand(e, e.Message.MentionedUsers, AddRoleFunc, "add");
        }

        [Command]
        [Group("role")]
        [Description("Removes a role to all mentioned users. Requires ``Manage Role`` permission for u1 and bot.")]
        [Parameter("Role")]
        [Parameter("Users", ParameterType.Multiple)]
        [Check(typeof(ProdChecker))]
        [Check(typeof(ManageRolesChecker))]
        public static void Remove(CommandEventArgs e) {
            RoleCommand(e, e.Message.MentionedUsers, RemoveRoleFunc, "remove");
        }

        [Command]
        [Group("role")]
        [Description("Nukes all of a certain role from all members of the server")]
        [Parameter("Role")]
        [Check(typeof(ProdChecker))]
        [Check(typeof(ManageRolesChecker))]
        public static void Nuke(CommandEventArgs e) {
            RoleCommand(e, e.Server.Users, RemoveRoleFunc, "remove");
        }

        static async void RoleCommand(CommandEventArgs e, IEnumerable<User> users, Func<User, Role, Task> task, string action) {
            Role role = Utility.GetRole(e.GetArg("Role"), e.Server);
            await Command.ForEveryUser(e, users, 
                Command.AdminAction(e.Channel, action + " role", async user => await task(user, role)));
        }
    }
}
