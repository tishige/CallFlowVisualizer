
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using CommandLine;
using System.Text.RegularExpressions;
using ShellProgressBar;
using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Configuration;
using PureCloudPlatform.Client.V2.Model;
using System.Globalization;

namespace CallFlowVisualizer
{
    internal class Program
    {
		//Before running the program, please create a configuration file named 'appsettings.json' in the same directory as the program.
		static void Main(string[] args)
        {

            bool convertToVisio = false;
            bool convertToPng = false;
            bool disableAcceleration=false;
            int maxSecondDescriptionLengh = 50;

            //[ADD] 2024/10/27
            bool createFolderWithOrganizationName = false;
            string folderNameDateFormat = null;

            //[MOD] 2024/10/31 v1.7.1
            bool convertToDrawio = false;
            bool createParticipantDataList = false;

			//[ADD] 2025/05/06 v1.8.2
			bool createDataStepReference = false;


			List<string> flowTypeList = new();
            try
            {
                var configRoot = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile(path: "appsettings.json").Build();
                convertToVisio = configRoot.GetSection("drawioSettings").Get<DrawioSettings>().ConvertToVisio;
                convertToPng = configRoot.GetSection("drawioSettings").Get<DrawioSettings>().ConvertToPng;
                disableAcceleration = configRoot.GetSection("drawioSettings").Get<DrawioSettings>().DisableAcceleration;
                maxSecondDescriptionLengh = configRoot.GetSection("cfvSettings").Get<CfvSettings>().MaxSecondDescriptionLengh;

                flowTypeList = configRoot.GetSection("cfvSettings").Get<CfvSettings>().flowTypeList;

				//[ADD] 2024/10/27
				createFolderWithOrganizationName = configRoot.GetSection("cfvSettings").Get<CfvSettings>().CreateFolderWithOrganizationName;
				folderNameDateFormat = configRoot.GetSection("cfvSettings").Get<CfvSettings>().FolderNameDateFormat;


				convertToDrawio = configRoot.GetSection("drawioSettings").Get<DrawioSettings>().ConvertToDrawio;
				createParticipantDataList = configRoot.GetSection("cfvSettings").Get<CfvSettings>().CreateParticipantDataList;
				createDataStepReference = configRoot.GetSection("cfvSettings").Get<CfvSettings>().CreateDataStepReference;



			}
			catch (Exception)
            {
                ColorConsole.WriteError($"The configuration file 'appsettings.json' was not found in this directory.");
                PrintUsage();

            }

            if(maxSecondDescriptionLengh<=0 || maxSecondDescriptionLengh >= 1025)
            {
                ColorConsole.WriteError($"maxSecondDescriptionLength should be from 1 to 1024.");
                PrintUsage();

            }

			//[ADD] 2024/10/27
			if (createFolderWithOrganizationName && !String.IsNullOrEmpty(folderNameDateFormat))
			{
				try
				{
					if (folderNameDateFormat.Contains("mm"))
					{
						throw new FormatException($"The format '{folderNameDateFormat}' is incorrect. 'mm' represents minutes. Use 'MM' for months.");
					}

					string dateString = DateTime.Now.ToString(folderNameDateFormat);

					if (!DateTime.TryParseExact(dateString, folderNameDateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dateTime))
					{
						throw new FormatException($"Failed to parse the date string '{dateString}' with the format '{folderNameDateFormat}'");
					}
				}
				catch (Exception e)
				{
					ColorConsole.WriteError($"Incorrect date format: {e.Message}");
					PrintUsage();
					throw;
				}


			}

			var parseResult = Parser.Default.ParseArguments<Options>(args);
            Options opt = new();

            string mode = "";
            FileInfo[] fileInfo = null;
            List<string> jsonFiles = null;
            switch (parseResult.Tag)
            {
                case ParserResultType.Parsed:
                    var parsed = parseResult as Parsed<Options>;
                    opt = parsed.Value;

                    if (convertToVisio) opt.visio = true;
                    if (convertToPng) opt.png = true;
					//[MOD] 2024/10/31 v1.7.1
					if (convertToDrawio) opt.drawio = true;

                    if (opt.Filename != null && opt.flowId != null)
                    {
                        ColorConsole.WriteError("Incorrect command line argument.");
                        PrintUsage();

                    }

                    // [ADD] 2023/06/29
                    if (opt.Filename != null && opt.flowName != null)
                    {
                        ColorConsole.WriteError("Enclose flow name in double quotation marks.");
                        PrintUsage();

                    }

                    if (opt.drawio || opt.visio || opt.png)
                    {
                        Process[] processes = Process.GetProcessesByName("draw.io");
                        if (processes.Length > 0)
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

                    if ((opt.flowId != null || opt.flowName != null) && args != null)
                    {
                        Regex regEx = new Regex(@"(^([0-9A-Fa-f]{8}[-][0-9A-Fa-f]{4}[-][0-9A-Fa-f]{4}[-][0-9A-Fa-f]{4}[-][0-9A-Fa-f]{12})$)");

                        if(opt.flowName != null && opt.flowId != null)
                        {
                            ColorConsole.WriteError($"Set either Architect's flow ID or flow Name.");
                            PrintUsage();
                        }

                        IEnumerable<string> notContained = opt.flowType.Except(flowTypeList);

                        // [ADD] 2023/06/29
                        if (opt.flowType != null && notContained.Any()) 
                        {
                            var notContainedValue = String.Join(",",notContained.ToArray());
                            ColorConsole.WriteError($"Incorrect flow type [{notContainedValue}].");
                            PrintUsage();
                        }


                        if ((opt.flowId!=null && regEx.Match(opt.flowId).Success) || opt.flowId == "all" || opt.flowName!=null)
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
            List<string> jsonFileListPD = new();
            switch (mode)
            {

                case "PureConnect":

                    List<PureConnectFlowElements> flowElementsList = CollectDSValuesFromXML.CollectDS(opt.Filename);
                    ColorConsole.WriteLine("Creating CSV file for PureConnect", ConsoleColor.Yellow);
                    csvFileResultList = CreateCSV.CreateCSVPureConnect(flowElementsList);

                    Console.WriteLine();
                    break;

                case "GenesysCloud":

                    List<string> jsonFileList = new() { opt.Filename };
                    csvFileResultList = GcJSONtoCSV.gcJsonToCSV(jsonFileList, opt);
                    jsonFileListPD = jsonFileList;
                    break;

                case "FetchFromGenesysCloud":

                    FetchGCAccessToken.GetAccessToken(opt.profile);
                    // [ADD] 2023/06/29
                    if (opt.flowId != null)
                    {
                        jsonFileList = FetchFlows.CreateArchitectJSONFile(opt.flowId,opt.flowType,opt.profile);

                    }
                    else
                    {
                        jsonFileList = FetchFlows.CreateArchitectJSONFileWithName(opt.flowName, opt.flowType,opt.profile);

                    }
                    csvFileResultList = GcJSONtoCSV.gcJsonToCSV(jsonFileList, opt);
                    jsonFileListPD = jsonFileList;

                    Console.WriteLine();
                    break;

                case "architect":

                    csvFileResultList = GcJSONtoCSV.gcJsonToCSV(jsonFiles, opt);
                    jsonFileListPD = jsonFiles;
                    break;

                default:

                    PrintUsage();
                    break;

            }

            if (opt.drawio || opt.visio || opt.png)
            {
                DrawFlow.DrawFlowFromCSV(csvFileResultList, opt.visio, opt.png, disableAcceleration,opt.profile,opt.architect);

            }

			//[MOD] 2024/10/31 v1.7.1
			if (opt.createParticipantDataList || createParticipantDataList)
            {
                GcJSONtoCSV.gcJsonToPDListCSV(jsonFileListPD);
            }

			//[ADD] 2025/05/06 v1.8.2
			if (opt.reference || createDataStepReference)
			{
				GcJSONtoCSV.gcJsonToDATAStepsListCSV(jsonFileListPD);
			}

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
            sb.AppendLine(@"  -f --fetch     Fetch latest Architect flow of specified flowID from GenesysCloud and Create CSV files for drawio.");
            sb.AppendLine(@"  -n --name      Fetch latest Architect flow of specified flowName from GenesysCloud and Create CSV files for drawio.");
            sb.AppendLine(@"  -t --type      Fetch latest Architect flow of specified flowType from GenesysCloud and Create CSV files for drawio.");
            sb.AppendLine(@"                 bot,commonmodule,digitalbot,inboundcall,inboundchat,inboundemail,inboundshortmessage,outboundcall");
            sb.AppendLine(@"                 inqueuecall,inqueueemail,inqueueshortmessage,speech,securecall,surveyinvite,voice,voicemail,workflow,workitem");
            sb.AppendLine(@"  -a --architect Create CSV files for json files in .\Architect folder.");
            sb.AppendLine(@"  -p --profile   [PROFILE NAME] Change GenesysCloud organization.Use [default] if not specified.");
            sb.AppendLine(@"  -v --visio     Convert to visio file after creating drawio files");
            sb.AppendLine(@"  -g --png       Convert to png file after creating drawio files");
            sb.AppendLine(@"  -l --list      Create Participant Data CSV list");
			sb.AppendLine(@"  -r --reference Create reference documents for flow data actions and data table lookups");
			sb.AppendLine(@"  --help         Show this screen.");
            sb.AppendLine(@"  --version      Show version.");
            sb.AppendLine(@"  --debug        Show node id of architect on diagram.");

            sb.AppendLine();
            sb.AppendLine("Examples:");
            sb.AppendLine("  CallFlowVisualizer.exe PCSampleFlow.xml");
            sb.AppendLine("  CallFlowVisualizer.exe GCSampleFlow.json -d");
            sb.AppendLine("  CallFlowVisualizer.exe -f all");
            sb.AppendLine("  CallFlowVisualizer.exe -f 3a3d264a-978e-4a1d-abc9-e1f76c556f1a -v -p prod-org");
            sb.AppendLine("  CallFlowVisualizer.exe -n MarketingDev -t call -v -p prod-org");
            sb.AppendLine();

            Console.Out.Write(sb.ToString());
            Environment.Exit(1);
        }

    }


}




