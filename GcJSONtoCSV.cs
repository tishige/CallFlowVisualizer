using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using ShellProgressBar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using NLog;

namespace CallFlowVisualizer
{
    class GcJSONtoCSV
    {
        /// <summary>
        /// Load GenesysCloud JSON file and create CSV file
        /// </summary>
        /// <param name="filePathList"></param>
        internal static List<string> gcJsonToCSV(List<string> filePathList, Options opt)
        {

            var configRoot = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile(path: "appsettings.json").Build();
            
            bool appendGcFlowTypeToFileName = configRoot.GetSection("cfvSettings").Get<CfvSettings>().AppendGcFlowTypeToFileName;
            bool appendGcOrgNameToFileName = configRoot.GetSection("cfvSettings").Get<CfvSettings>().AppendGcOrgNameToFileName;
            bool createFlowPerReusabletask = configRoot.GetSection("cfvSettings").Get<CfvSettings>().CreateFlowPerReusabletask;
            bool createPagePerReusabletask = configRoot.GetSection("cfvSettings").Get<CfvSettings>().CreatePagePerReusabletask;

            // v1.5.0
            bool showExpression = configRoot.GetSection("cfvSettings").Get<CfvSettings>().ShowExpression;
            bool showPromptDetail = configRoot.GetSection("cfvSettings").Get<CfvSettings>().ShowPromptDetail;


            List<string> csvFileResultList = new();

            Console.WriteLine();
            ColorConsole.WriteLine("Creating CSV file for GenesysCloud", ConsoleColor.Yellow);
            
            var pboptions = new ProgressBarOptions
            {
                ProgressCharacter = '─',
                ProgressBarOnBottom = true
            };

            var csvpb = new ProgressBar(filePathList.Count(), "Creating CSV file", pboptions);

            foreach (var jsonFilePath_i in filePathList)
            {

                List<GenesysCloudFlowNode> flowNodesList = CollectGCValuesFromJSON.CollectNode(jsonFilePath_i);

                if(showPromptDetail)
                {
                    flowNodesList = FetchAudioPrompt.FetchPromptDescription(flowNodesList,opt);

                }


                var json = File.ReadAllText(jsonFilePath_i);
                JObject result;
                using (var sr = new StreamReader(jsonFilePath_i, Encoding.UTF8))
                {
                    var jsonData = sr.ReadToEnd();
                    result = (JObject)JsonConvert.DeserializeObject(jsonData);
                }

                string flowName = result["name"].ToString().Replace(" ", "_").Replace("&","and");

                if (appendGcFlowTypeToFileName)
                {
                    flowName = "(" + result["type"].ToString() + ")_" + flowName;

                }

                if (appendGcOrgNameToFileName)
                {
                    string fileName = Path.GetFileNameWithoutExtension(jsonFilePath_i);
                    string orgName = null;
                    Regex regOrg = new Regex(@"^(\[.+\]).+");

                    Match matchOrg = regOrg.Match(fileName);
                    if (matchOrg.Success)
                    {
                        GroupCollection group = matchOrg.Groups;
                        orgName = group[1].Value;
                    }

                    flowName =orgName + flowName;

                }

                Regex regEx = new Regex(@"(([0-9A-Fa-f]{8}[-][0-9A-Fa-f]{4}[-][0-9A-Fa-f]{4}[-][0-9A-Fa-f]{4}[-][0-9A-Fa-f]{12}))");
                string flowId = regEx.Match(jsonFilePath_i).Value;

                csvpb.Tick(flowName);

                foreach (char c in Path.GetInvalidFileNameChars())
                {

                    flowName = flowName.Replace(c, '_');
                }

                // [ADD-1]2023/03/25
                if (createFlowPerReusabletask)
                {
                    var flowNodeListGrouped = flowNodesList.GroupBy(x=>x.FlowGroup).ToList();

                    foreach (var flowNodeListGrouped_i in flowNodeListGrouped)
                    {
                        string flowGroupName = flowNodeListGrouped_i.Key;

                        foreach (char c in Path.GetInvalidFileNameChars())
                        {
                            flowGroupName = flowGroupName.Replace(c, '_');
                        }

                        flowGroupName = flowGroupName.Replace(' ', '_');
                        List<GenesysCloudFlowNode> flowNodesListPerTask = new();
                        flowNodesListPerTask = flowNodeListGrouped_i.ToList();
                        csvFileResultList.Add(CreateCSV.CreateCSVGenCloud(flowNodesListPerTask, flowName, flowId, opt.debug, flowGroupName));

                    }

                }
                else if (createPagePerReusabletask) //[ADD] 2023/03/31
                {
                    csvFileResultList.Add(CreateCSV.CreateCSVGenCloudPerPage(flowNodesList, flowName, flowId, opt.debug, null));

                }else
                {
                    csvFileResultList.Add(CreateCSV.CreateCSVGenCloud(flowNodesList, flowName, flowId, opt.debug,null));

                }


            }

            Console.WriteLine();

            return csvFileResultList;

        }

        internal static void gcJsonToPDListCSV(List<string> filePathList)
        {

            Console.WriteLine();
            ColorConsole.WriteLine("Creating Participant Data List of Architect flow", ConsoleColor.Yellow);

            var pboptions = new ProgressBarOptions
            {
                ProgressCharacter = '─',
                ProgressBarOnBottom = true
            };

            var csvpb = new ProgressBar(filePathList.Count(), "Creating Participant Data List CSV file", pboptions);

            List<GenesysCloudParticipantData> gcPDList = new();

            foreach (var jsonFilePath_i in filePathList)
            {

                var pdResult = CollectGCParticipantData.CollectParticipantData(jsonFilePath_i);
                if(pdResult != null)
                {
                    gcPDList.AddRange(pdResult);

                }

                csvpb.Tick(jsonFilePath_i);
                Console.WriteLine();

            }

            CreateCSV.CreatePDListCSVGenCloud(gcPDList);
            Console.WriteLine();


        }


    }
}
