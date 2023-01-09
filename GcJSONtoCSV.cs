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

                csvFileResultList.Add(CreateCSV.CreateCSVGenCloud(flowNodesList, flowName, flowId, opt.debug));

            }

            Console.WriteLine();

            return csvFileResultList;

        }

    }
}
