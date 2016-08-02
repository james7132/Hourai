using System;

namespace DrumBot {
    public class PermissionException : Exception {

        public PermissionException(string error) : base(error) {
        }

    }
}
