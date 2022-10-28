using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsseblerBuildHelper
{
    [Serializable]
    internal class CfgProperties
    {
        internal string MASMPath { get; set; }
        internal string OutputPath { get; set; }
        internal string LastSrcPath { get; set; }
        internal bool DontGenObj { get; set; }
        internal bool ConvertToCp { get; set; }
        internal bool OpenProgramAfterBuild { get; set; }
        internal bool SaveLog { get; set; }

    }
    internal static class Config
    {
        internal static CfgProperties properties { get; set; }

        internal static void Load()
        {
            if (properties != null)
                return;

            if (File.Exists(".\\properties.dat"))
            {
                properties = ReadFromBinaryFile<CfgProperties>(".\\properties.dat");
            }
            else
            {
                properties = new CfgProperties();
                properties.SaveLog = true;
                WriteToBinaryFile<CfgProperties>(".\\properties.dat", properties);
            }
        }

        internal static void Save()
        {
            WriteToBinaryFile(".\\properties.dat", properties);
        }

        public static void WriteToBinaryFile<T>(string filePath, T objectToWrite, bool append = false)
        {
            using (Stream stream = File.Open(filePath, append ? FileMode.Append : FileMode.Create))
            {
                var binaryFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                binaryFormatter.Serialize(stream, objectToWrite);
            }
        }

        public static T ReadFromBinaryFile<T>(string filePath)
        {
            using (Stream stream = File.Open(filePath, FileMode.Open))
            {
                var binaryFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                return (T)binaryFormatter.Deserialize(stream);
            }
        }
    }
}
