using Microsoft.Extensions.Configuration;
using ShellProgressBar;
using System.Diagnostics;
using System.Runtime.InteropServices;


namespace CallFlowVisualizer
{
    internal class DrawFlow
    {
        internal static void DrawFlowFromCSV(List<string> csvFileResultList, bool visio, bool png, bool disableAcceleration)
        {
            ColorConsole.WriteLine($"Creating drawio diagram", ConsoleColor.Yellow);
            string currentPath = Directory.GetCurrentDirectory();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                if (!File.Exists(Path.Combine(currentPath, "drawio", "draw.io.app", "Contents/MacOS/draw.io")))
                {
                    ColorConsole.WriteError(@"drawio.io.app does not exist.Create drawio folder and Copy /drawio-desktop/dist/mac/draw.io.app into it.");
                    Environment.Exit(1);

                }

            }
            else
            {
                if (!File.Exists(Path.Combine(currentPath, "drawio", "draw.io.exe")))
                {
                    ColorConsole.WriteError(@"drawio.io.exe does not exist.Create drawio folder and Copy all files in \drawio-desktop\dist\win-unpacked into it.");
                    Environment.Exit(1);

                }

            }

            // Is this draw.io is for this app?
            ProcessStartInfo startInfo = new();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                startInfo.FileName = Path.Combine(currentPath, "drawio", "draw.io.app", "Contents/MacOS/draw.io");

            }
            else
            {
                startInfo.FileName = Path.Combine(currentPath, "drawio", "draw.io.exe");

            }

            startInfo.Arguments = "--help";
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            Process p = Process.Start(startInfo);
            p.WaitForExit();
            var helpmsg = p.StandardOutput.ReadToEnd();

            if (!helpmsg.Contains("--csv"))
            {
                ColorConsole.WriteError(@"This drawio.io.exe does not support CSV import.Get draw.io.exe for CallflowVisualizer and Copy all files in \drawio-desktop\dist\win-unpacked into it.");
                Environment.Exit(1);

            }

            // CSV file exists?
            var csvFiles = Directory.GetFiles(Path.Combine(currentPath, "csv"), "*.csv");

            if (csvFiles.Length == 0)
            {
                ColorConsole.WriteError(@"CSV files does not exites.Load JSON or XML files or fetch architect files.");
                Environment.Exit(1);

            }

            // Start to read CSV file
            var pboptions = new ProgressBarOptions
            {
                ProgressCharacter = '─',
                ProgressBarOnBottom = true
            };

            var drawpb = new ProgressBar(csvFileResultList.Count(), "Drawing flow", pboptions);

            var configRoot = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile(path: "appsettings.json").Build();
            int maxRetryCount = configRoot.GetSection("drawioSettings").Get<DrawioSettings>().MaxRetryCount;

            foreach (var csvFile_i in csvFileResultList)
            {

                startInfo = new ProcessStartInfo();

                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    startInfo.FileName = Path.Combine(currentPath, "drawio", "draw.io.app", "Contents/MacOS/draw.io");

                }
                else
                {
                    startInfo.FileName = Path.Combine(currentPath, "drawio", "draw.io.exe");

                }

                //20230319 --disable-acceleration

                if (disableAcceleration)
                {
                    startInfo.Arguments = "-i " + csvFile_i + " --disable-acceleration";

                }
                else
                {
                    startInfo.Arguments = "-i " + csvFile_i;

                }


                drawpb.Tick(csvFile_i);
                p = Process.Start(startInfo);
                p.WaitForExit();

                if (IsDrawioFileSizeZero(currentPath, csvFile_i))
                {
                    while (true)
                    {
                        ColorConsole.WriteWarning("Creating drawio file was failed. Retrying...");
                        p = Process.Start(startInfo);
                        p.WaitForExit();

                        if (!IsDrawioFileSizeZero(currentPath, csvFile_i))
                        {
                            break;

                        }
                        else
                        {
                            if (--maxRetryCount == 0)
                            {
                                ColorConsole.WriteError($"Please Try again later {csvFile_i}");
                                break;

                            }
                        }

                    }

                }

            }

            Console.WriteLine();

            // Convert to visio
            if (visio)
            {
                ColorConsole.WriteLine("Convert to VISIO", ConsoleColor.Yellow);
                var visiopb = new ProgressBar(csvFileResultList.Count(), "Convert to VISIO", pboptions);

                if (!Directory.Exists(Path.Combine(currentPath, "visio")))
                    Directory.CreateDirectory(Path.Combine(currentPath, "visio"));

                foreach (var csvFile_i in csvFileResultList)
                {

                    string drawioFile = Path.ChangeExtension(Path.GetFileName(csvFile_i), ".drawio");
                    drawioFile = Path.Combine(currentPath, "flow", drawioFile);
                    string outPutFolder = Path.Combine(currentPath, "visio");

                    startInfo = new ProcessStartInfo();

                    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        startInfo.FileName = Path.Combine(currentPath, "drawio", "draw.io.app", "Contents/MacOS/draw.io");

                    }
                    else
                    {
                        startInfo.FileName = Path.Combine(currentPath, "drawio", "draw.io.exe");

                    }

                    if (disableAcceleration)
                    {
                        startInfo.Arguments = "-x -f vsdx " + drawioFile + " -o " + outPutFolder + " --disable-acceleration";
                    }
                    else
                    {
                        startInfo.Arguments = "-x -f vsdx " + drawioFile + " -o " + outPutFolder;

                    }



                    visiopb.Tick(drawioFile);
                    p = Process.Start(startInfo);
                    p.WaitForExit();

                }

                Console.WriteLine();

            }

            // Convert to png
            if (png)
            {
                ColorConsole.WriteLine("Convert to png", ConsoleColor.Yellow);
                var pngpb = new ProgressBar(csvFileResultList.Count(), "Convert to png", pboptions);

                if (!Directory.Exists(Path.Combine(currentPath, "png")))
                    Directory.CreateDirectory(Path.Combine(currentPath, "png"));

                foreach (var csvFile_i in csvFileResultList)
                {

                    string drawioFile = Path.ChangeExtension(Path.GetFileName(csvFile_i), ".drawio");
                    drawioFile = Path.Combine(currentPath, "flow", drawioFile);
                    string outPutFolder = Path.Combine(currentPath, "png");

                    startInfo = new ProcessStartInfo();

                    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        startInfo.FileName = Path.Combine(currentPath, "drawio", "draw.io.app", "Contents/MacOS/draw.io");

                    }
                    else
                    {
                        startInfo.FileName = Path.Combine(currentPath, "drawio", "draw.io.exe");

                    }

                    if (disableAcceleration)
                    {
                        startInfo.Arguments = "-x -f png " + drawioFile + " -o " + outPutFolder + " --disable-acceleration";
                    }
                    else
                    {
                        startInfo.Arguments = "-x -f png " + drawioFile + " -o " + outPutFolder;

                    }

                    pngpb.Tick(drawioFile);
                    p = Process.Start(startInfo);
                    p.WaitForExit();

                }

                Console.WriteLine();

            }

        }

        /// <summary>
        /// Is draw.io file size is zero
        /// </summary>
        /// <param name="currentPath"></param>
        /// <param name="csvfileName"></param>
        /// <returns></returns>
        private static bool IsDrawioFileSizeZero(string currentPath, string csvfileName)
        {
            FileInfo file = new FileInfo(Path.Combine(currentPath, "flow", Path.GetFileNameWithoutExtension(csvfileName) + ".drawio"));
            try
            {
                if (file.Length == 0)
                {
                    return true;

                }
            }
            catch (Exception)
            {
                return true;


            }



            return false;
        }


    }
}
