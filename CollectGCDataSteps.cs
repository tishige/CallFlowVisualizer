using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Collections;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
using PureCloudPlatform.Client.V2.Model;
using NLog.Targets;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Data;

namespace CallFlowVisualizer
{
    internal class CollectGCDataSteps
    {
        internal static NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        internal static List<GenesysCloudDataStep> CollectGCDataStep(string jsonPath)
        {
            var json = File.ReadAllText(jsonPath);
            JObject result;

            Logger.Info($"Analyzing file: {jsonPath}");

            using (var sr = new StreamReader(jsonPath, Encoding.UTF8))
            {
                var jsonData = sr.ReadToEnd();

                try
                {
                    result = (JObject)JsonConvert.DeserializeObject(jsonData);
                }
                catch (Exception e)
                {
                    // Mostly Not UTF-8
                    Logger.Error(e);
                    Environment.Exit(1);
                    throw;
                }

            }

            JArray flowSeqItemList = result["flowSequenceItemList"].ToObject<JArray>();


            List<GenesysCloudDataStep> dataStepsList = CollectDataStepsValues(flowSeqItemList, jsonPath);

            return dataStepsList;
        }


        private static List<GenesysCloudDataStep> CollectDataStepsValues(JArray flowSeqItemList, string jsonPath)
        {

            List<string> StartActionIdList = flowSeqItemList.Where(x => (string)x["startAction"] != null).Select(y => y.Value<string>("id")).ToList();
           
            string jsonFileName = Path.GetFileNameWithoutExtension(jsonPath);

            string orgName = null;
            string flowType = null;
            string flowID = null;
            string architectFlowname = null;

            Regex regOrg = new Regex(@"^(\[.+\]).+");
            Match matchOrg = regOrg.Match(jsonFileName);
            if (matchOrg.Success)
            {
                GroupCollection group = matchOrg.Groups;
                orgName = group[1].Value.Replace("[","").Replace("]","");
                jsonFileName = jsonFileName.Replace(group[1].Value, "");
            }

            Regex regFlowType = new Regex(@"(\(.+\)).+");
            Match matcFlowType = regFlowType.Match(jsonFileName);
            if (matcFlowType.Success)
            {
                GroupCollection group = matcFlowType.Groups;
                flowType = group[1].Value.Replace("(", "").Replace(")", "");
                jsonFileName = jsonFileName.Replace(group[1].Value, "");
            }



            Regex regFlowID = new Regex(@"([0-9A-Fa-f]{8}[-][0-9A-Fa-f]{4}[-][0-9A-Fa-f]{4}[-][0-9A-Fa-f]{4}[-][0-9A-Fa-f]{12})$");
            if (regFlowID.Match(jsonFileName).Success)
            {
                flowID = regFlowID.Match(jsonFileName).Value;
                jsonFileName = jsonFileName.Replace(flowID, "");
            }


            Regex regArchitectName = new Regex(@"((?:^_)(.+)(?:_$))|(.+)");
            Match matcArchitectName = regArchitectName.Match(jsonFileName);
            if (matcArchitectName.Success)
            {
                GroupCollection group = matcArchitectName.Groups;
                if (!String.IsNullOrEmpty(group[2].Value))
                {
                    architectFlowname = group[2].Value;

                }
                else if (!String.IsNullOrEmpty(group[3].Value))
                {
                    architectFlowname = group[3].Value;
                }

            }
            else
            {
                architectFlowname = jsonFileName;
            }

            string taskName = null;

			List<GenesysCloudDataStep> dataStepList = new();

			if (StartActionIdList.Count == 0)
            {
				GenesysCloudDataStep dataStep = new();
				dataStep.OrgName = orgName;
				dataStep.FlowType = flowType;
				dataStep.FlowID = flowID;
				dataStep.ArchitectFlowName = architectFlowname;
				dataStep.FlowName = taskName;
				dataStepList.Add(dataStep);
				return dataStepList;

            }
			

			foreach (var flowSeqItem in flowSeqItemList)
            {
                taskName = (string)flowSeqItem["name"];

				// Task flow
				if (flowSeqItem["actionList"] != null)
                {
                    var actionList = flowSeqItem.SelectToken("actionList").ToList();
				
					foreach (var action_i in actionList)
                    {

						//[ADD-1]2025/05/06
						GenesysCloudDataStep dataStep = new();
						dataStep.OrgName = orgName;
						dataStep.FlowType = flowType;
						dataStep.FlowID = flowID;
						dataStep.ArchitectFlowName = architectFlowname;
						dataStep.FlowName = taskName;
						dataStep.Id = action_i["trackingId"].ToString();
						dataStep.Name = action_i["name"].ToString();
						dataStep.Type = action_i["__type"].ToString();

						if ((string)action_i["__type"] == "DataTableLookupAction")
						{

                            dataStep.DataTableId = action_i["datatableId"].ToString();
                            dataStep.DataTableName = action_i["datatableName"].ToString();
                            string lookupKeyValue = action_i["lookupKeyValue"]["text"].ToString();
                            dataStep.inputs.Add(new Inputs() { name = "lookupKeyValue", text = lookupKeyValue });

							var outputList = action_i["outputs"].ToList();

							List<Outputs> outputsList = new List<Outputs>();

							foreach (var output_i in outputList)
							{

								var name = output_i["name"].ToString();
                                var text = output_i["value"]["text"].ToString();

								Outputs outputs = new();
                                outputs.name = name;
                                outputs.text = text;
								outputsList.Add(outputs);

								dataStep.outputs = outputsList;

							}
							dataStepList.Add(dataStep);

						}
						else if ((string)action_i["__type"] == "DataAction")
						{
                            dataStep.DataActionName = action_i["actionName"].ToString();
							var inputList = action_i["inputs"].ToList();
							List<Inputs> inputsList = new List<Inputs>();

							foreach (var input_i in inputList)
							{
								var name = input_i["name"].ToString();
								var text = input_i["value"]["text"].ToString();
								Inputs inputs = new();
								inputs.name = name;
								inputs.text = text;
								inputsList.Add(inputs);
								dataStep.inputs = inputsList;
							}

							var outputList = action_i["outputs"].ToList();
							List<Outputs> outputsList = new List<Outputs>();

							foreach (var output_i in outputList)
							{
								var name = output_i["name"].ToString();
								var text = output_i["value"]["text"].ToString();
								Outputs outputs = new();
								outputs.name = name;
								outputs.text = text;
								outputsList.Add(outputs);
								dataStep.outputs = outputsList;
							}
							dataStepList.Add(dataStep);
						}
						else
						{
							continue;
						}
					}

				}

			}


			return dataStepList;
        }




    }



}



