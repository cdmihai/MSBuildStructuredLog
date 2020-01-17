using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace StructuredLogger.Tests
{
    public class TestUtilities
    {
        public static string GetFullPath(string fileName)
        {
            return Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), fileName);
        }
    }
}
