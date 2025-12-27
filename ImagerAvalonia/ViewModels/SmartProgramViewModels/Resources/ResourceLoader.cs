using System;
using System.IO;
using System.Reflection;

namespace ImagerAvalonia.PythonEditor.Resources
{
    internal class ResourceLoader
    {

        internal static string LoadSampleFile(string fileName)
        {
            string filePath = fileName;

            if (!File.Exists(filePath))
                return string.Empty;

            return File.ReadAllText(filePath);
        }
    }
}