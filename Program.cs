
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using CommandLine;
using System.Text.RegularExpressions;
using ShellProgressBar;
using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace CallFlowVisualizer
{
    internal class Program
    {

        static void Main(string[] args)
        {

            bool convertToVisio = false;
            bool convertToPng = false;

            try
            {
                var configRoot = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile(path: "appsettings.json").Build();
                convertToVisio = configRoot.GetSection("drawioSettings").Get<DrawioSettings>().ConvertToVisio;
                convertToPng = configRoot.GetSection("drawioSettings").Get<DrawioSettings>().ConvertToPng;

            }
            catch (Exception)
            {
                ColorConsole.WriteError($"The configuration file 'appsettings.json' was not found in this directory.");
                PrintUsage();

            }


            var parseResult = Parser.Default.ParseArguments<Options>(args);
            Options opt= new();

            string mode = "";
            FileInfo[] fileInfo=null;
            List<string> jsonFiles = null;
            switch (parseResult.Tag)
            {
                case ParserResultType.Parsed:
                    var parsed = parseResult as Parsed<Options>;
                    opt = parsed.Value;

                    if (convertToVisio) opt.visio = true;
                    if (convertToPng) opt.png = true;

                    if (opt.Filename != null && opt.flowId != null)
                    {
                        ColorConsole.WriteError("Incorrect command line argument.");
                        PrintUsage();

                    }

                    if (opt.drawio || opt.visio || opt.png)
                    {
                        Process[] processes = Process.GetProcessesByName("draw.io");
                        if(processes.Length > 0)
                        {
                            ColorConsole.WriteError($"draw.io is running. Close draw.io first.");
                            Environment.Exit(1);
                        }

                    }

                    if (opt.Filename != null)
                    {
                        if (!File.Exists(opt.Filename))
                        {
                            ColorConsole.WriteError($"{opt.Filename} does not exist.  Check if the file exists.");
                            PrintUsage();
                        }

                        if (Path.GetExtension(opt.Filename) == ".json")
                        {
                            mode = "GenesysCloud";
                            break;
                        }

                        if (Path.GetExtension(opt.Filename) == ".xml")
                        {
                            mode = "PureConnect";
                            break;
                        }

                    }

                    if (opt.flowId !=null && args!=null)
                    {
                        Regex regEx = new Regex(@"(^([0-9A-Fa-f]{8}[-][0-9A-Fa-f]{4}[-][0-9A-Fa-f]{4}[-][0-9A-Fa-f]{4}[-][0-9A-Fa-f]{12})$)");
                        if (regEx.Match(opt.flowId).Success || opt.flowId=="all")
                        {
                            mode = "FetchFromGenesysCloud";
                            break;
                        }
                        else
                        {
                            ColorConsole.WriteError($"Argument is not a GUID format. {opt.flowId} Set Architect's flow ID.");
                            PrintUsage();
                        }

                    }


                    if (opt.architect)
                    {
                        mode = "architect";

                        try
                        {
                            DirectoryInfo di = new DirectoryInfo(Path.Combine(Directory.GetCurrentDirectory(), "Architect"));
                            fileInfo = di.GetFiles("*.json", SearchOption.AllDirectories);

                            if (fileInfo.Length == 0)
                            {
                                ColorConsole.WriteError($"No json files were found in Architect folder. Run fetch command first.");
                                PrintUsage();
                            }
                            else
                            {
                                jsonFiles = fileInfo.Select(x => x.FullName).ToList();

                            }

                        }
                        catch (Exception)
                        {
                            ColorConsole.WriteError($"No Architect folder.Run fetch command first.");
                            PrintUsage();
                        }

                        break;

                    }

                    PrintUsage();
                    break;

                case ParserResultType.NotParsed:

                    PrintUsage();
                    break;

            }

            ColorConsole.WriteLine($"Mode : {mode} started.", ConsoleColor.Yellow);

            List<string> csvFileResultList = new List<string>();

            switch (mode)
            {

                case "PureConnect":

                    List<PureConnectFlowElements> flowElementsList = CollectDSValuesFromXML.CollectDS(opt.Filename);
                    ColorConsole.WriteLine("Creating CSV file for PureConnect", ConsoleColor.Yellow);
                    csvFileResultList = CreateCSV.CreateCSVPureConnect(flowElementsList);

                    Console.WriteLine();
                    break;

                case "GenesysCloud":

                    List<string> jsonFileList = new(){ opt.Filename };
                    csvFileResultList =GcJSONtoCSV.gcJsonToCSV(jsonFileList,opt);
                    break;

                case "FetchFromGenesysCloud":

                    FetchGCAccessToken.GetAccessToken(opt.profile);
                    jsonFileList = FetchFlows.CreateArchitectJSONFile(opt.flowId);
                    csvFileResultList= GcJSONtoCSV.gcJsonToCSV(jsonFileList, opt);

                    Console.WriteLine();
                    break;

                case "architect":

                    csvFileResultList = GcJSONtoCSV.gcJsonToCSV(jsonFiles, opt);
                    break;

                default:

                    PrintUsage();
                    break;

            }

            if (opt.drawio || opt.visio || opt.png)
            {
                DrawFlow.DrawFlowFromCSV(csvFileResultList,opt.visio,opt.png);

            }

            Console.WriteLine();

            Console.WriteLine();
            ColorConsole.WriteLine("Completed!", ConsoleColor.Yellow);
            Environment.Exit(0);
        }


        private static void PrintUsage()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine();

            sb.AppendLine("Usage:");
            sb.AppendLine("  CallFlowVisualizer.exe <DSEDIT XML File> | <Architect JSON File>");
            sb.AppendLine("  CallFlowVisualizer.exe -f <flowId> | <all>");

            sb.AppendLine();
            sb.AppendLine("Options:");
            sb.AppendLine(@"  -d --drawio    Call .\drawio\draw.io.exe for CallFlowVisualizer after creating CSV files.");
            sb.AppendLine(@"  -f --fetch     Fetch latest Architect flow from GenesysCloud and Create CSV files for drawio.");
            sb.AppendLine(@"  -a --architect Create CSV files for json files in .\Architect folder.");
            sb.AppendLine(@"  -p --profile   [PROFILE NAME] Change GenesysCloud organization.Use [default] if not specified.");
            sb.AppendLine(@"  -v --visio     Convert to visio file after creating drawio files");
            sb.AppendLine(@"  -n --png       Convert to png file after creating drawio files");
            sb.AppendLine(@"  --help         Show this screen.");
            sb.AppendLine(@"  --version      Show version.");
            sb.AppendLine(@"  --debug        Show node id of architect on diagram.");

            sb.AppendLine();
            sb.AppendLine("Examples:");
            sb.AppendLine("  CallFlowVisualizer.exe PCSampleFlow.xml");
            sb.AppendLine("  CallFlowVisualizer.exe GCSampleFlow.json -d");
            sb.AppendLine("  CallFlowVisualizer.exe -f all");
            sb.AppendLine("  CallFlowVisualizer.exe -f 3a3d264a-978e-4a1d-abc9-e1f76c556f1a -v -p prod-org");
            sb.AppendLine();

            Console.Out.Write(sb.ToString());
            Environment.Exit(1);
        }



    }


}




