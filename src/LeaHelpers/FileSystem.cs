using System.IO;

namespace Leayal.Shared.Windows
{
    public static class FileSystem
    {
        public static void MoveOverwrite_AwareReadOnly(string sourceFilepath, string destinationFilepath)
        {
            if (File.Exists(destinationFilepath))
            {
                var attr = File.GetAttributes(destinationFilepath);
                if ((attr & FileAttributes.ReadOnly) != 0)
                {
                    File.SetAttributes(destinationFilepath, attr & ~FileAttributes.ReadOnly);
                }
                File.Delete(destinationFilepath);
            }
            File.Move(sourceFilepath, destinationFilepath, true);
        }
    }
}
