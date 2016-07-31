using System;

namespace DrumBot {

    [AttributeUsage(AttributeTargets.Method)]
    public class CommandAttribute : Attribute {
        public string Name { get; set; }

        public CommandAttribute(string name) { Name = name; }
    }
}
