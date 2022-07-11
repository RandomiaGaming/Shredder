using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
namespace Shredder
{
    public enum ShreddingMethod { Zeros, Ones, Random };
    public static class Program
    {
        public static readonly Random RNG = new Random();
        [STAThread]
        public static void Main()
        {
            ShredFiles(ShowOpenFileDialoge(), LoadSettings());
        }
        public static List<string> ShowOpenFileDialoge()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog()
            {
                AddExtension = true,
                CheckFileExists = true,
                CheckPathExists = true,
                DereferenceLinks = false,
                Filter = "All files(*.*)|*.*",
                FilterIndex = 0,
                InitialDirectory = "C:\\",
                Multiselect = true,
                ReadOnlyChecked = true,
                RestoreDirectory = true,
                ShowReadOnly = false,
                SupportMultiDottedExtensions = true,
                ValidateNames = true,
                Title = "Select Files To Shred...",
                ShowHelp = false,
            };

            DialogResult result = openFileDialog.ShowDialog();

            if (result == DialogResult.Abort || result == DialogResult.Cancel || result == DialogResult.Ignore || result == DialogResult.No || result == DialogResult.None || result == DialogResult.Retry || openFileDialog.FileNames is null || openFileDialog.FileNames.Length <= 0)
            {
                return new List<string>();
            }
            else
            {
                return new List<string>(openFileDialog.FileNames);
            }
        }
        public static void ShredFolderRecursively(string folderPath, Settings settings)
        {
            if (folderPath == null)
            {
                throw new Exception("Folder path cannot be null.");
            }
            if (folderPath == "")
            {
                throw new Exception("Folder path cannot be empty.");
            }
            if (!Directory.Exists(folderPath))
            {
                throw new Exception("Folder does not exist.");
            }
            foreach (string filePath in Directory.GetFiles(folderPath))
            {
                ShredFile(filePath, settings);
            }
            foreach (string subFolderPath in Directory.GetDirectories(folderPath))
            {
                ShredFolderRecursively(subFolderPath, settings);
            }
        }
        public static void ShredFolder(string folderPath, Settings settings)
        {
            foreach (string filePath in Directory.GetFiles(folderPath))
            {
                ShredFile(filePath, settings);
            }
        }
        public static void ShredFiles(List<string> filePaths, Settings settings)
        {
            foreach (string filePath in filePaths)
            {
                ShredFile(filePath, settings);
            }
        }
        public static void ShredFile(string filePath, Settings settings)
        {
            FileStream fileStream = File.Open(filePath, FileMode.Open);
            for (int p = 1; p <= settings.shreddingPasses; p++)
            {
                fileStream.Seek(0, SeekOrigin.Begin);
                byte[] buffer = new byte[fileStream.Length];
                switch (settings.shreddingMethod)
                {
                    case ShreddingMethod.Zeros:
                        for (int i = 0; i < buffer.Length; i++)
                        {
                            buffer[i] = byte.MinValue;
                        }
                        break;
                    case ShreddingMethod.Ones:
                        for (int i = 0; i < buffer.Length; i++)
                        {
                            buffer[i] = byte.MaxValue;
                        }
                        break;
                    case ShreddingMethod.Random:
                        RNG.NextBytes(buffer);
                        break;
                    default:
                        RNG.NextBytes(buffer);
                        break;
                }
                fileStream.Write(buffer, 0, buffer.Length);
                fileStream.Flush();
            }
            fileStream.Close();
            fileStream.Dispose();
            File.Delete(filePath);
        }
        public static void CreateSettingsFileIfMissing()
        {
            if (!File.Exists(GetSettingsFilePath()))
            {
                File.WriteAllText(GetSettingsFilePath(), Settings.DefaultSettings.Serialize());
            }
        }
        public static string GetSettingsFilePath()
        {
            return Path.GetDirectoryName(Assembly.GetCallingAssembly().Location) + @"\ShredderSettings.txt";
        }
        public static Settings LoadSettings()
        {
            CreateSettingsFileIfMissing();
            Settings output = new Settings(File.ReadAllText(GetSettingsFilePath()));
            return output;
        }
    }
    public sealed class Settings
    {
        public static Settings DefaultSettings
        {
            get
            {
                return new Settings();
            }
        }
        public ShreddingMethod shreddingMethod = ShreddingMethod.Random;
        public int shreddingPasses = 16;
        private Settings()
        {
            shreddingMethod = ShreddingMethod.Random;
            shreddingPasses = 3;
        }
        public Settings(ShreddingMethod shreddingMethod, int shreddingPasses)
        {
            this.shreddingMethod = shreddingMethod;
            if (shreddingPasses < 1)
            {
                throw new Exception("Shredding passes cannot be less than one.");
            }
            this.shreddingPasses = shreddingPasses;
        }
        public Settings(string serializedData)
        {
            if (serializedData == null)
            {
                throw new Exception("Serialized data cannot be null.");
            }
            if (serializedData == "")
            {
                throw new Exception("Serialized data cannot be empty.");
            }
            serializedData = CleanSettingsString(serializedData);
            List<string> statementStrings = SliceIntoStatementStrings(serializedData);
            List<Statement> statements = DeserializeStatements(statementStrings);
            foreach (Statement statement in statements)
            {
                switch (statement.targetVariable)
                {
                    case "shreddingMethod":
                        switch (statement.targetValue)
                        {
                            case "ShreddingMethod.Zeros":
                                shreddingMethod = ShreddingMethod.Zeros;
                                break;
                            case "ShreddingMethod.Ones":
                                shreddingMethod = ShreddingMethod.Ones;
                                break;
                            case "ShreddingMethod.Random":
                                shreddingMethod = ShreddingMethod.Random;
                                break;
                        }
                        break;
                    case "shreddingPasses":
                        try
                        {
                            int rawShreddingPasses = int.Parse(statement.targetValue);
                            if (rawShreddingPasses < 1)
                            {
                                throw new Exception("Shredding passes cannot be less than one.");
                            }
                            shreddingPasses = rawShreddingPasses;
                        }
                        catch
                        {

                        }
                        break;
                }
            }
        }
        private static List<Statement> DeserializeStatements(List<string> statements)
        {
            List<Statement> output = new List<Statement>();
            foreach (string statement in statements)
            {
                try
                {
                    output.Add(DeserializeStatement(statement));
                }
                catch
                {

                }
            }
            return output;
        }
        private static Statement DeserializeStatement(string statement)
        {
            string targetVariable = "";
            string targetValue = "";
            bool foundEquals = false;
            for (int i = 0; i < statement.Length; i++)
            {
                if (statement[i] == '=')
                {
                    if (foundEquals)
                    {
                        throw new ArgumentException();
                    }
                    else
                    {
                        foundEquals = true;
                    }
                }
                else
                {
                    if (foundEquals)
                    {
                        targetValue += statement[i];
                    }
                    else
                    {
                        targetVariable += statement[i];
                    }
                }
            }
            if (targetVariable == "" || targetValue == "")
            {
                throw new ArgumentException();
            }
            return new Statement(targetVariable, targetValue);
        }
        private static List<string> SliceIntoStatementStrings(string settings)
        {
            List<string> output = new List<string>();
            string currentStatement = "";

            for (int i = 0; i < settings.Length; i++)
            {
                if (settings[i] == ';')
                {
                    output.Add(currentStatement);
                    currentStatement = "";
                }
                else
                {
                    currentStatement += settings[i];
                }
            }

            if (currentStatement != "")
            {
                output.Add(currentStatement);
            }

            return output;
        }
        private static string CleanSettingsString(string settingsString)
        {
            string output = "";
            for (int i = 0; i < settingsString.Length; i++)
            {
                if ("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz1234567890=;.".Contains(settingsString[i].ToString()))
                {
                    output += settingsString[i];
                }
                else
                {

                }
            }
            return output;
        }
        public string Serialize()
        {
            List<string> statements = new List<string>();
            switch (shreddingMethod)
            {
                case ShreddingMethod.Zeros:
                    statements.Add("shreddingMethod = ShreddingMethod.Zeros;");
                    break;
                case ShreddingMethod.Ones:
                    statements.Add("shreddingMethod = ShreddingMethod.Ones;");
                    break;
                case ShreddingMethod.Random:
                    statements.Add("shreddingMethod = ShreddingMethod.Random;");
                    break;
                default:
                    statements.Add("shreddingMethod = ShreddingMethod.Random;");
                    break;
            }
            statements.Add($"shreddingPasses = {shreddingPasses};");
            string output = "";
            for (int i = 0; i < statements.Count; i++)
            {
                output += statements[i];
                if (i != statements.Count - 1)
                {
                    output += "\n";
                }
            }
            return output;
        }
    }
    public struct Statement
    {
        public string targetVariable;
        public string targetValue;
        public Statement(string targetVariable, string targetValue)
        {
            if (targetVariable == null || targetValue == null)
            {
                throw new NullReferenceException();
            }
            if (targetVariable == "" || targetValue == "")
            {
                throw new ArgumentException();
            }
            this.targetVariable = targetVariable;
            this.targetValue = targetValue;
        }
    }
}
