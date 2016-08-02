using System;

namespace DrumBot {
    
    /// <summary>
    /// Marks a command as a part of a group
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class GroupAttribute : Attribute {
        public string Name { get; }

        public GroupAttribute(string name ) { Name = name; }
    }
}
