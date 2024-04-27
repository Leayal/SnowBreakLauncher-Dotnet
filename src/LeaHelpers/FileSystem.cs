using System.IO;

namespace Leayal.Shared.Windows
{
    public static class FileSystem
    {
        public static void MoveOverwrite_AwareReadOnly(string sourceFilepath, string destinationFilepath)
        {
            ForceDelete(destinationFilepath);
            File.Move(sourceFilepath, destinationFilepath, true);
        }

        public static void ForceDelete(string filePath)
        {
            if (File.Exists(filePath))
            {
                var attr = File.GetAttributes(filePath);
                if ((attr & FileAttributes.ReadOnly) != 0)
                {
                    File.SetAttributes(filePath, attr & ~FileAttributes.ReadOnly);
                }
                File.Delete(filePath);
            }
        }
    }
}
