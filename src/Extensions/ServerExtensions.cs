using System.Collections.Generic;
using System.Linq;
using Discord;

namespace DrumBot {
    public static class ServerExtensions {

        public static Role GetRole(this Server server, string roleName) {
            Role role = server.FindRoles(roleName).FirstOrDefault();
            if (role == null)
                throw new RoleNotFoundException(roleName);
            return role;
        }

        public static IEnumerable<Role> Order(this IEnumerable<Role> roles) => 
            roles.Where(r => r != r.Server.EveryoneRole)
                .OrderBy(r => r.Position);

        public static IEnumerable<Role> OrderAlpha(this IEnumerable<Role> roles) => 
            roles.Where(r => r != r.Server.EveryoneRole)
                .OrderBy(r => r.Name);

        public static IEnumerable<Channel> Order(this IEnumerable<Channel> channels) => 
            channels.OrderBy(c => c.Position);

        public static IEnumerable<Channel> OrderAlpha(this IEnumerable<Channel> channels) => 
            channels.OrderBy(c => c.Name);
    }
}
