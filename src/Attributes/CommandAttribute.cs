using System;

namespace DrumBot {

    /// <summary>
    /// Attribute that marks a static method for use as a command.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class CommandAttribute : Attribute {
        public string Name { get; set; }

        public CommandAttribute() : this(null) { }
        public CommandAttribute(string name) { Name = name; }
    }
}
