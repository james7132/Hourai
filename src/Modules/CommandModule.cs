using Discord;
using Discord.Commands;
using Discord.Commands.Permissions;
using Discord.Commands.Permissions.Visibility;
using Discord.Modules;

namespace DrumBot {

    public class MinimumRoleChecker : IPermissionChecker {
        string roleType;

        public MinimumRoleChecker(string roleType) { this.roleType = roleType; }

        public bool CanRun(Discord.Commands.Command command,
                           User user,
                           Channel channel,
                           out string error) {
            error = string.Empty;
            if (user.IsBotOwner() || user.IsServerOwner())
                return true;
            var server = channel.Server;
            var serverConfig = Config.GetServerConfig(server);
            ulong? minRole = serverConfig.GetMinimumRole(roleType);
            if (minRole == null) {
                error = $"{user.Mention} is not the server owner, and no minimum role for {roleType.Code()} is set.";
                return false;
            }
            var role = server.GetRole(minRole.Value);
            if(role == null) {
                error = $"{server.Owner.Mention} the role for {roleType.Code()} no longer exists, and you are the only one who can now run it.";
                return false;
            }
            if(!Utility.RoleCheck(user, role))
                error = $"{user.Mention} you do not have the minimum role to run this command. You need at least the {role.Name.Code()} to run it.";
            return error.IsNullOrEmpty();
        }
    }

    public class CommandModule : IModule {
        public void Install(ModuleManager manager) {
            manager.Client.ServerAvailable += (s, e) => {
                manager.CreateCommands(cbg => {
                    var serverConfig = Config.GetServerConfig(e.Server);
                    foreach (var command in serverConfig.Commands) {
                        command.CreateCommand(cbg, e.Server);
                    }
                });
            };
            manager.CreateCommands("command",
                cbg => {
                    cbg.PublicOnly();
                    cbg.CreateCommand()
                        .Description("Creates a custom command. Deletes an existing one if response is empty")
                        .Parameter("Name")
                        .Parameter("Response", ParameterType.Unparsed)
                        .AddCheck(new MinimumRoleChecker("command"))
                        .Do(Command.Response(e => {
                            var name = e.GetArg("Name");
                            var response = e.GetArg("Response");
                            var serverConfig = Config.GetServerConfig(e.Server);
                            var command = serverConfig.GetCustomCommand(name);
                            if (string.IsNullOrEmpty(response)) {
                                if (command == null)
                                    return $"Command {name.Code()} does not exist and thus cannot be deleted.";
                                serverConfig.RemoveCustomCommand(name);
                                return $"Custom command {name.Code()} has been deleted.";
                            }
                            string action;
                            if (command == null) {
                                command = serverConfig.AddCustomCommand(name);
                                command.Response = response;
                                command.CreateCommand(cbg, e.Server);
                                action = "created";
                            } else {
                                command.Response = response;
                                action = "updated";
                            }
                            serverConfig.Save();
                            return Utility.Success($"Command {name.Code()} {action} with response {response}.");
                        }));

                    cbg.CreateCommand("role")
                        .Description("Sets the minimum role for creating custom commands.")
                        .Parameter("Role")
                        .AddCheck(new ServerOwnerChecker())
                        .Do(Command.Response(e => {
                            var role = e.Server.GetRole(e.GetArg("Role"));
                            var serverConfig = Config.GetServerConfig(e.Server);
                            serverConfig.SetMinimumRole("command", role);
                            return Utility.Success($"Set {role.Name.Code()} as the minimum role to create custom commnds");
                        }));
                });
        }
    }
}
