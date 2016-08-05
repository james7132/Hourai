using System;

namespace DrumBot {
    public class NotFoundException : Exception {
        public NotFoundException(string type, string role) : base($"No {type} named { role } found.") {
        }
    }

    public class RoleRankException : Exception {
        public RoleRankException(string message) : base(message) {
        }
    }
}
