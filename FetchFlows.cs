using NLog;
using PureCloudPlatform.Client.V2.Api;
using PureCloudPlatform.Client.V2.Client;
using PureCloudPlatform.Client.V2.Model;
using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Configuration;
using ShellProgressBar;

namespace CallFlowVisualizer
{

    internal class FetchFlows
    {
        internal static NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        internal static List<string> CreateArchitectJSONFile(string flowId)
        {
            var configRoot = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile(path: "appsettings.json").Build();

            ArchitectApi gcArchApi = new();
            var page = 1;
            int pageSize = configRoot.GetSection("gcSettings").Get<GcSettings>().PageSize;
            int pageCount;
            float prog;

            bool appendGcFlowTypeToFileName = configRoot.GetSection("cfvSettings").Get<CfvSettings>().AppendGcFlowTypeToFileName;
            bool appendGcOrgNameToFileName = configRoot.GetSection("cfvSettings").Get<CfvSettings>().AppendGcOrgNameToFileName;

            List<FlowData> flowDataList = new();

            // Create Architect folder
            string currentPath = Directory.GetCurrentDirectory();
            if (!Directory.Exists(Path.Combine(currentPath, "Architect")))
                Directory.CreateDirectory(Path.Combine(currentPath, "Architect"));

            // org name
            OrganizationApi gcOrgApi = new();
            Organization result = gcOrgApi.GetOrganizationsMe();
            string orgName = result.Name;

            var pboptions = new ProgressBarOptions
            {
                ProgressCharacter = '─',
                ProgressBarOnBottom = true
            };

            // Fetch all flow
            if (flowId == "all")
            {
                ColorConsole.WriteLine($"Fetch Flow Entities from [{orgName}] pageSize:{pageSize}", ConsoleColor.Yellow);

                using ProgressBar progressBar = new ProgressBar(10000, "Fetch Flow Entities", pboptions);
                IProgress<float> progress = progressBar.AsProgress<float>();

                FlowEntityListing flowEntityListing = new();
                try
                {
                    do
                    {
                        Logger.Info($"Fetch Flow Entities Page:{page}");

                        flowEntityListing = gcArchApi.GetFlows(pageNumber: page, pageSize: pageSize);
                        pageCount = (int)flowEntityListing.PageCount;

                        foreach (var item in flowEntityListing.Entities)
                        {

                            FlowData flowData = new();
                            flowData.Id = item.Id.ToString();
                            flowData.Name = item.Name.ToString();
                            flowDataList.Add(flowData);

                        }
                        prog = ((float)page / pageCount);// * 100;
                        progress.Report(prog);
                        
                        page++;

                    } while (page <= pageCount);


                }
                catch (Exception e)
                {
                    Debug.Print("Exception when calling Architect.GetFlows: " + e.Message);
                    ColorConsole.WriteError("Exception when calling Architect.GetFlows: " + e.Message);
                    Environment.Exit(1);

                }

                Logger.Info($"Fetch all flow completed!");

            }
            else //Fetch one flow
            {
                FlowData flowData = new();
                flowData.Id = flowId;
                flowData.Name = "";
                flowDataList.Add(flowData);
                Logger.Info($"Fetch {flowData.Id} flow completed!");

            }

            ColorConsole.WriteLine($"Fetch Architect Latest Configuration from [{orgName}] Number of flows:{flowDataList.Count()}", ConsoleColor.Yellow);
            var fdlpb = new ProgressBar(flowDataList.Count(), "Fetch Architect Latest Configuration", pboptions);

            // Save JSON file
            List<string> jsonFileList = new();
            JObject flowResponse = new();

            // [B]2023/01/19 fixed
            orgName = orgName.Replace(" ", "");

            foreach (var item in flowDataList)
            {

                Logger.Info($"Fetch Flow {item.Id}");

                try
                {
                    //Need to use Newtonsoft version 12.0.1 to avoid Max Depth 64 depth problem.
                    flowResponse = (JObject)gcArchApi.GetFlowLatestconfiguration(item.Id);

                }
                catch (Exception e)
                {
                    Debug.Print("Exception when calling Architect.GetFlowLatestconfiguration: " + e.Message);
                    ColorConsole.WriteError("Exception when calling Architect.GetFlowLatestconfiguration: " + e.Message);
                    Environment.Exit(1);

                }

                string flowName = flowResponse["name"].ToString().Replace(" ", "_").Replace("&", "and");

                foreach (char c in Path.GetInvalidFileNameChars())
                {

                    flowName = flowName.Replace(c, '_');
                }

                if (appendGcFlowTypeToFileName)
                {
                    flowName = "("+flowResponse["type"].ToString()+")_"+ flowName;

                }

                if (appendGcOrgNameToFileName)
                {
                    flowName = "[" + orgName + "]" + flowName;

                }


                string filePath = Path.Combine(currentPath, "Architect", flowName + "_"+item.Id+".json");

                fdlpb.Tick(flowName);

                using (StreamWriter file = File.CreateText(filePath))
                {
                    Logger.Info($"Save to disk {filePath}");
                    JsonSerializer serializer = new();
                    serializer.Serialize(file, flowResponse);
                    jsonFileList.Add(filePath);
                }

            }

            Console.WriteLine();
            return jsonFileList;

        }

        private class FlowData
        {
            internal string Id { get; set; } = null!;
            internal string Name { get; set; } = null!;

        }


    }
}
