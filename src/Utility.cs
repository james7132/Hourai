using System;
using System.Linq;
using Discord;

namespace DrumBot {

    public class RoleNotFoundException : Exception {
        public RoleNotFoundException(string role) : base($"No role named { role } found.") {
        }
    }
    public static class Utility {

        public static Role GetRole(string roleName, Server server) {
            Role role = server.FindRoles(roleName).FirstOrDefault();
            if (role == null)
                throw new RoleNotFoundException(roleName);
            //RoleCheck(server.GetUser(Bot.Client.CurrentUser.Id), role, action);
            //RoleCheck(caller, role, action);
            return role;
        }

        public static void RoleCheck(User user, Role role, string action) {
            int position = role.Position;
            if (user.Server.Owner != user && user.Roles.Max(r => r.Position) <= position)
                throw new Exception($"{user.Name} cannot {action} role \"{role.Name}\", as it is above their role.");
        }

        public static string DateString(DateTime date) {
            return date.ToString("yyyy-MM-dd hh:mm:ss");
        }

    }
}
