using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Modules;

namespace DrumBot {
    public class RoleModule : IModule {
        readonly Func<User, Role, Task> _addRoleFunc = async (user, role) => await user.AddRoles(role);
        readonly Func<User, Role, Task> _removeRoleFunc = async (user, role) => await user.RemoveRoles(role);
        const string RoleParam = "Role";
        const string UserParam = "User(s)";
        const string Requirements =
            "Requires ``Manage Role`` permission for both user and bot.";

        public void Install(ModuleManager manager) {
            manager.UserUpdated +=
                async delegate(object s, UserUpdatedEventArgs e) {
                    var serverConfig = Config.GetServerConfig(e.Server);
                    if (!serverConfig.AllowCommands)
                        return;
                    await serverConfig.GetUserConfig(e.After).RemoveBannedRoles(e.After);
                };
            manager.CreateCommands("role", cbg => {
                cbg.Category("Admin");
                cbg.AddCheck(Check.ManageRoles());
                RoleCommandBuilder(cbg, "ban", "Bans all mentioned users from a specified role")
                    .Do(async delegate(CommandEventArgs e) {
                        var serverConfig = Config.GetServerConfig(e.Server);
                        await RoleCommand(e, "ban", e.Message.MentionedUsers, async (u, r) => serverConfig.GetUserConfig(u).BanRole(r));
                    });

                RoleCommandBuilder(cbg, "unban", "Unban all mentioned users from a specified role")
                    .Do(async delegate(CommandEventArgs e) {
                        var serverConfig = Config.GetServerConfig(e.Server);
                        await RoleCommand(e, "ban", e.Message.MentionedUsers, async (u, r) => serverConfig.GetUserConfig(u).UnbanRole(r));
                    });

                RoleCommandBuilder(cbg, "create", "Creates a mentionable role and applies it to all mentioned users")
                    .Do(async delegate(CommandEventArgs e) {
                        await e.Server.CreateRole(e.GetArg("Role"));
                        await Add(e);
                    });

                RoleCommandBuilder(cbg, "delete", "Deletes a role from the server", false)
                    .Do(async delegate(CommandEventArgs e) {
                        await e.Server.GetRole(e.GetArg("Role")).Delete();
                        await e.Success();
                    });

                RoleCommandBuilder(cbg, "add", "Adds a role to all mentioned users. Requires ``Manage Role`` permission for u1 and bot.")
                    .Do(Add);

                RoleCommandBuilder(cbg, "remove", "Removes a role to all mentioned users.")
                    .Do(async e => await RoleCommand(e, "remove", e.Message.MentionedUsers, _removeRoleFunc));

                RoleCommandBuilder(cbg, "nuke", "Nukes all of a certain role from all members of the server")
                    .Do(async e => await RoleCommand(e, "remove", e.Server.Users, _removeRoleFunc));
            });
        }

        CommandBuilder RoleCommandBuilder(CommandGroupBuilder builder, string name, string description, bool requireUsers = true) {
            var command = builder.CreateCommand(name)
                                 .Description(description + Requirements)
                                 .Parameter(RoleParam);
            if (requireUsers)
                command = command.Parameter(UserParam, ParameterType.Multiple);
            return command;
        }

        async Task Add(CommandEventArgs e) {
            await RoleCommand(e, "add", e.Message.MentionedUsers, _addRoleFunc);
        }

        static async Task RoleCommand(CommandEventArgs e, string action, IEnumerable<User> users, Func<User, Role, Task> task) {
            Role role = e.Server.GetRole(e.GetArg("Role"));
            if(!Utility.RoleCheck(e.Server.CurrentUser, role))
                throw new RoleRankException($"I cannot {action} role \"{role.Name}\", as it is above my roles.");
            if(!Utility.RoleCheck(e.User, role))
                throw new RoleRankException($"{e.User.Name} cannot {action} role \"{role.Name}\", as it is above their roles.");
            await Command.ForEvery(e, users,
                Command.Action(e.Channel, action + " role", async user => await task(user, role)));
        }
    }
}
