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

namespace CallFlowVisualizer
{
    internal class CollectGCParticipantData
    {
        internal static NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        internal static List<GenesysCloudParticipantData> CollectParticipantData(string jsonPath)
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


            List<GenesysCloudParticipantData> participantDataList = CollectPDValues(flowSeqItemList, jsonPath);

            return participantDataList;
        }


        private static List<GenesysCloudParticipantData> CollectPDValues(JArray flowSeqItemList, string jsonPath)
        {

            List<string> StartActionIdList = flowSeqItemList.Where(x => (string)x["startAction"] != null).Select(y => y.Value<string>("id")).ToList();
            List<GenesysCloudParticipantData> participantDataList = new();
           
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

            if (StartActionIdList.Count == 0)
            {
                return null;

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
                        GenesysCloudParticipantData participantData = new();

                        if ((string)action_i["__type"] == "GetAttributesAction")
                        {
                            participantData.OrgName = orgName;
                            participantData.FlowType = flowType;
                            participantData.FlowID = flowID;
                            participantData.ArchitectFlowName = architectFlowname;
                            participantData.FlowName = taskName;
                            participantData.Id= action_i["trackingId"].ToString();
                            participantData.Name= action_i["name"].ToString();
                            participantData.Type= action_i["__type"].ToString();

                            var attrVariables = action_i["variables"];
                            List<PDVariables> pdValiablesList = new();

                            foreach (var attrVariables_i in attrVariables)
                            {
                                PDVariables pdVariables = new();
                                var exp = attrVariables_i["expression"]["text"].ToString();
                                var val = attrVariables_i["variable"]["text"].ToString();
                                pdVariables.ExpressionText = exp;
                                pdVariables.VariableText = val;
                                pdVariables.Statement = exp + "=" + val;

                                if (attrVariables_i["expression"]["metaData"].Any())
                                {

                                    var metaRefList = attrVariables_i["expression"]["metaData"]["references"].ToList();
                                    foreach (var metaRefList_i in metaRefList)
                                    {
                                        MetaData metaData = new();

                                        metaData.MetaRefId = metaRefList_i["id"].ToString();
                                        metaData.MetaName = metaRefList_i["name"].ToString();

                                        pdVariables.MetaDataList.Add(metaData);
                                    }

                                }
                                pdValiablesList.Add(pdVariables);

                            }

                            participantData.Variables = pdValiablesList;
                            participantDataList.Add(participantData);

                        }

                        if ((string)action_i["__type"] == "SetAttributesAction")
                        {
                            participantData.OrgName = orgName;
                            participantData.FlowType = flowType;
                            participantData.FlowID = flowID;
                            participantData.ArchitectFlowName = architectFlowname;
                            participantData.FlowName = taskName;
                            participantData.Id = action_i["trackingId"].ToString();
                            participantData.Name = action_i["name"].ToString();
                            participantData.Type = action_i["__type"].ToString();
                            var attrVariables = action_i["variables"].ToList();
                            List<PDVariables> pdValiablesList = new();

                            foreach (var attrVariables_i in attrVariables)
                            {
                                PDVariables pdVariables= new();
                                var exp = attrVariables_i["expression"]["text"].ToString();
                                var val = attrVariables_i["variable"]["text"].ToString();
                                pdVariables.ExpressionText= exp;
                                pdVariables.VariableText= val;
                                pdVariables.Statement = val + "=" + exp;


                                if (attrVariables_i["expression"]["metaData"].Any())
                                {

                                    var metaRefList = attrVariables_i["expression"]["metaData"]["references"].ToList();
                                    foreach (var metaRefList_i in metaRefList)
                                    {

                                        MetaData metaData = new();

                                        metaData.MetaRefId = metaRefList_i["id"].ToString();
                                        metaData.MetaName = metaRefList_i["name"].ToString();

                                        pdVariables.MetaDataList.Add(metaData);

                                    }

                                }

                                pdValiablesList.Add(pdVariables);

                            }
                            participantData.Variables = pdValiablesList;
                            participantDataList.Add(participantData);

                        }

                    }


                }

                // Basic menu flow
                if (flowSeqItem["menuChoiceList"] != null)
                {

                    foreach (var menuChoise_i in flowSeqItem["menuChoiceList"])
                    {
                        GenesysCloudParticipantData participantData = new();

                        if (menuChoise_i["action"]["resultData"]!=null)
                        {
                            participantData.OrgName = orgName;
                            participantData.FlowType = flowType;
                            participantData.FlowID = flowID;
                            participantData.ArchitectFlowName = architectFlowname;
                            participantData.FlowName = taskName;
                            participantData.Id = menuChoise_i["action"]["trackingId"].ToString();
                            participantData.Name = menuChoise_i["action"]["name"].ToString();
                            participantData.Type = menuChoise_i["action"]["__type"].ToString();
                            List<PDVariables> pdValiablesList = new();

                            PDVariables pdVariables = new();

                            var metaRefList = menuChoise_i["action"]["resultData"]["metaData"]["references"].ToList();
                            foreach (var metaRefList_i in metaRefList)
                            {

                                MetaData metaData = new();
                                metaData.MetaRefId = metaRefList_i["id"].ToString();
                                metaData.MetaName = metaRefList_i["name"].ToString();
                                pdVariables.MetaDataList.Add(metaData);

                            }

                            pdValiablesList.Add(pdVariables);

                            participantData.Variables = pdValiablesList;
                            participantDataList.Add(participantData);

                        }


                    }

                }

            }



            return participantDataList;
        }




    }



}



