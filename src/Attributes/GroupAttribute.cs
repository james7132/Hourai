using System;

namespace DrumBot {
    [AttributeUsage(AttributeTargets.Method)]
    public class GroupAttribute : Attribute {
        public string Name { get; }

        public GroupAttribute(string name ) { Name = name; }
    }
}
