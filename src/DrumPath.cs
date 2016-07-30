using System;
using System.IO;
using System.Reflection;

namespace DrumBot {
    [InitializeOnLoad]
    public class DrumPath {
        static DrumPath() {
            string codeBase = Assembly.GetExecutingAssembly().CodeBase;
            var uri = new UriBuilder(codeBase);
            string path = Uri.UnescapeDataString(uri.Path);
            ExecutionDirectory = Path.GetDirectoryName(path);
            Log.Info($"Execution Directory: { ExecutionDirectory }");
        }

        public static string ExecutionDirectory { get; }
    }
}
