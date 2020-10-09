using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.IO.Compression;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Globalization;

namespace convertLogs
{
    class Program
    {
        static string Version => "0.9";
        static string InputDir = null;
        static string OutputDir = null;
        static Output OutputType = Output.None;

        static void Main(string[] args)
        {
            Console.WriteLine($"-- convertLogs v{Version} --\n");
            
            //drag and drop
            if (args.Length == 1)
                args = new string[] { "-f", args[0] };
            
            if (GetArgs(args) && CheckPreconditions())
            {
                Console.WriteLine($"Extracting to directory {OutputDir}...\n");
                DecompressAndConvert(InputDir, OutputDir, OutputType);
                Console.WriteLine("\nComplete.");
            }
        }

        static bool GetArgs(string[] args)
        {
            Dictionary<string, string> options = new Dictionary<string, string>();
            if (args.Length == 0)
            {
                Console.WriteLine($"    ARGUMENTS");
                Console.WriteLine($"    -f <input directory or zip file> (required)");
                Console.WriteLine($"    -o <output directory> (auto-generated if not included)");
                Console.WriteLine(@$"    -t <output type> (""none"" or ""json"", defaults to ""none"")");
                return false;
            }
            else if (args.Length % 2 != 0)
            {
                Console.WriteLine("ERROR: Invalid argument count.");
                return false;
            }
            else
            {
                for (int i = 0; i < args.Length; i += 2)
                    options[args[i]] = args[i + 1];

                if (!options.ContainsKey("-f"))
                {
                    Console.WriteLine($"ERROR: Input directory cannot be null");
                    return false;
                }
                else
                {
                    InputDir = options["-f"];
                }

                OutputDir = options.ContainsKey("-o") ? options["-o"] : InputDir + "_converted";
                OutputDir = OutputDir.Replace(".zip_converted", "_converted");
                OutputType = (Output)(options.ContainsKey("-t") ? Enum.Parse(typeof(Output), options["-t"], true) : Output.None);
                return true;
            }
        }

        static bool CheckPreconditions()
        {
            List<string> errors = new List<string>();

            try
            {
                Path.GetFullPath(InputDir);
            }
            catch
            {
                Console.WriteLine($@"Invalid input path ""{InputDir}""");
            }

            try
            {
                Path.GetFullPath(OutputDir);
            }
            catch
            {
                Console.WriteLine($@"Invalid output path ""{OutputDir}""");
            }

            
            if (File.Exists(InputDir))
            {
                if (!InputDir.EndsWith(".zip"))
                {
                    errors.Add($@"Invalid input file ""{InputDir}""");
                }       
            }
            else if (!Directory.Exists(InputDir))
            {
                errors.Add($@"Invalid input directory ""{InputDir}""");
            }

            OutputDir = GetOutputDirectory(OutputDir);
            Directory.CreateDirectory(OutputDir);

            if (errors.Count == 0)
                return true;

            foreach (string error in errors)
                Console.WriteLine("ERROR: " + error);
            return false;
        }

        static bool IsCompleteLog(string fileName)
        {
            return !fileName.Contains("part");
        }

        static string[] FileList(string path)
        {
            FileAttributes attr = File.GetAttributes(path);
            if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
                return Directory.GetFiles(path, "*", SearchOption.AllDirectories);
            else if (path.EndsWith(".zip", StringComparison.InvariantCultureIgnoreCase))
            {
                string outdir = GetOutputDirectory(path);
                ZipFile.ExtractToDirectory(path, outdir, true);
                return Directory.GetFiles(outdir, "*", SearchOption.AllDirectories);
            }
            else
                return new string[] { path };

        }

        static string GetOutputDirectory(string path)
        {
            string dirname = path.EndsWith(".zip") ? Path.GetFileNameWithoutExtension(path) : path;
            int i = 0;
            while (!DirectoryIsEmpty(MakeDirName(dirname, i)))
            {
                i++;
            }
            dirname = MakeDirName(dirname, i);
            return dirname;
        }

        static bool DirectoryIsEmpty(string dir)
        {
            if (!Directory.Exists(dir)) return true;
            if (Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Length == 0) return true;
            return false;
        }

        static string MakeDirName(string dirname, int i) => i > 0 ? dirname + "_" + i : dirname;

        static string[] ToArray(string path)
        {
            if (File.Exists(path))
            {
                return new string[] { path };
            }
            else
            {
                return Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);
            }
        }

        static string[] DecompressLogs(string[] filePaths)
        {
            List<string> compressedFiles = filePaths.Where(f => f.EndsWith(".gz")).ToList();
            List<string> decompressedFiles = filePaths.Where(f => !f.EndsWith(".gz")).ToList();

            foreach (string file in compressedFiles.ToArray())
            {
                string decompFile = Path.GetFileNameWithoutExtension(file);
                using (FileStream reader = File.OpenRead(file))
                using (GZipStream zip = new GZipStream(reader, CompressionMode.Decompress))
                using (StreamReader unzip = new StreamReader(zip))
                    File.WriteAllText(decompFile, unzip.ReadToEnd());

                compressedFiles.Remove(file);
                decompressedFiles.Add(decompFile);
            }

            return decompressedFiles.ToArray();
        }

        static DateTime? ExtractDate(string path)
        {
            string fileName = Path.GetFileName(path);
            string[] parts = fileName.Split('.');
            if (parts.Length < 6)
                return DateTime.MinValue;

            string dateStr = "";
            string formatStr = "";
            if (IsCompleteLog(path))
            {
                dateStr = string.Join(".", parts.Take(4));
                formatStr = "yyyy.M.d.H";  
            }
            else
            {
                dateStr = parts[3] + "." + parts[4];
                formatStr = "yyyy.M.d.H.mm";
            }
            try
            {
                return DateTime.ParseExact(dateStr, formatStr, CultureInfo.InvariantCulture);
            }
            catch
            {
                Console.WriteLine("Unable to extract date from " + path);
                return null;
            }
        }

        static void ConvertFile(string filePath, FileWriter writer)
        {
            string[] lines = File.ReadAllLines(filePath);
            string totalLine = "";
            foreach (string line in lines)
            {
                totalLine += line;
                if (line.EndsWith("}"))
                {
                    try
                    {
                        JObject data = JObject.Parse(totalLine.Replace("\n", ""));
                        writer.WriteToFile(data);
                        totalLine = "";
                    }
                    catch
                    {
                        continue;
                        // fail silently and try to parse with the next line
                    }
                }
            }
        }

        static void Convert(IEnumerable<string> filePaths, string outputDirectory, Output oType)
        {
            List<string> newPaths = filePaths.Where(f => ExtractDate(f).HasValue).ToList();
            newPaths.Sort((a, b) => DateTime.Compare(ExtractDate(a).Value, ExtractDate(b).Value));

            FileWriter writer = new FileWriter(outputDirectory, oType);
            foreach (string path in newPaths)
            {
                Console.WriteLine("Converting " + path);
                ConvertFile(path, writer);
            }
        }

        static void DecompressAndConvert(string path, string outputDirectory, Output oType)
        {
            string[] logs = FileList(path);
            string[] readyToConvert = DecompressLogs(logs);
            Convert(readyToConvert, outputDirectory, oType);
            foreach (string file in readyToConvert)
                File.Delete(file);
        }

        // -----------------------------------------------------------

        enum Output { None, Json }

        class FileWriter
        {
            public string OutputDirectory;
            public Dictionary<string, StreamWriter> OpenFiles = new Dictionary<string, StreamWriter>();
            public FileHelper Helper;

            public FileWriter(string outputDir, Output outputType)
            {
                OutputDirectory = outputDir;
                if (outputType == Output.Json)
                    Helper = new JsonFileHelper(outputDir);
                else
                    Helper = new FlatFileHelper(outputDir);
            }

            public StreamWriter GetOpenFile(string fileName)
            {
                if (!OpenFiles.ContainsKey(fileName))
                {
                    string dir = Path.GetDirectoryName(fileName);
                    if (!Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    OpenFiles[fileName] = new StreamWriter(Path.GetFullPath(fileName)) { AutoFlush = true };
                }

                return OpenFiles[fileName];
            }

            public void WriteToFile(JObject data)
            {
                string fileName = Helper.GetFileName(data);
                StreamWriter learnLog = GetOpenFile(fileName);
                Helper.Write(learnLog, data);

            }
        }

        abstract class FileHelper
        {
            public string OutputDir;

            public FileHelper(string outputDir)
            {
                OutputDir = outputDir;
            }

            public abstract void Write(StreamWriter fs, JObject data);

            public abstract string GetFileName(JObject data);
        }

        class FlatFileHelper : FileHelper
        {
            public FlatFileHelper(string outputDir) : base(outputDir) { }

            public override void Write(StreamWriter fs, JObject data)
            {
                fs.WriteLine(data["message"].ToString());
            }

            public override string GetFileName(JObject data)
            {
                string oldPath = "/usr/local/blackboard";
                string newPath = Path.Combine(Path.GetFullPath(OutputDir), data["host"].ToString());
                string path = data["path"].ToString();
                if (path.StartsWith(oldPath))
                {
                    return path.Replace(oldPath, newPath);
                }
                else
                {
                    return Path.Combine(newPath, path);
                }

            }

        }

        class JsonFileHelper : FileHelper
        {
            public JsonFileHelper(string outputDir) : base(outputDir) { }

            public override void Write(StreamWriter fs, JObject data)
            {
                fs.WriteLine(data.ToString(Formatting.None));
                fs.Flush();
            }

            public override string GetFileName(JObject data)
            {
                return Path.Combine(OutputDir, data["host"].ToString()) + "/logs.json";
            }
        }
    }
}
