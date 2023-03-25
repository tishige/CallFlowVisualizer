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
using System.Xml.Linq;
using Microsoft.Extensions.Configuration;

namespace CallFlowVisualizer
{
    internal class CollectGCValuesFromJSON
    {
        internal static NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        internal static List<GenesysCloudFlowNode> CollectNode(string jsonPath)
        {
            var json = File.ReadAllText(jsonPath);
            JObject result;

            Logger.Info($"Analyzing file: {jsonPath}");

            using (var sr = new StreamReader(jsonPath,Encoding.UTF8))
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

            string initialSequence = result["initialSequence"].ToString();

            string defaultLanguage=null;
            if (result["defaultLanguage"] != null)
            {
                defaultLanguage = result["defaultLanguage"].ToString();

            }

            // [C]2023/01/19 fixed
            string flowType = null;
            if (result["type"] != null)
            {
                flowType = result["type"].ToString();

            }

            // [ADD-3]2023/03/25
            var configRoot = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile(path: "appsettings.json").Build();
            int maxSecondDescriptionLengh = configRoot.GetSection("cfvSettings").Get<CfvSettings>().MaxSecondDescriptionLengh;
            bool showExpression = configRoot.GetSection("cfvSettings").Get<CfvSettings>().ShowExpression;


            JArray flowSeqItemList = result["flowSequenceItemList"].ToObject<JArray>();

            JArray reusableTaskList = null;
            var hasReusableTaskList = result["uiMetaData"]["task"];
            if (hasReusableTaskList != null)
            {
                // Rename reusable task to task.
                reusableTaskList = result["uiMetaData"]["task"].ToObject<JArray>();

            }

            // Create new branch nodes that are located just beneath of branch node.
            List<JToken> actionListBranchAdded = CreateBranchNode(flowSeqItemList, initialSequence, reusableTaskList, defaultLanguage);
            List<Tuple<string, string>> edges = new();
            Dictionary<string,BranchStatus> branchStatusTable = new();
            Dictionary<string,LoopStatus> loopStatusTable = new();


            foreach (var action_i in actionListBranchAdded)
            {

                Logger.Info($"actionListBranchAdded action_i: [{(string)action_i["trackingId"]}][{(string)action_i["name"]}] {(string)action_i["id"]}");

                // Do edge.Add nextAction first to analyze YES NO branch as it will be put into stack.
                if (action_i["nextAction"] != null)
                {
                    edges.Add(Tuple.Create((string)action_i["id"], (string)action_i["nextAction"]));
                    Logger.Info($"Create edges [nextAction] Add:{(string)action_i["id"]}=>{(string)action_i["nextAction"]}");

                }

                if (action_i["paths"] != null)
                {
                    if (!action_i["id"].ToString().Contains("__") && (string)action_i["__type"] != "AskForNLUNextIntentAction")
                    {
                        BranchStatus branchStatus = new();
                        branchStatus.NextAction= (string)action_i["nextAction"];
                        branchStatus.ExitPathTotalCount = action_i["paths"].Count();
                        
                        foreach (var eachPath in action_i["paths"])
                        {

                            ExitPathStatus exitPathStatus = new();
                            if (eachPath["outputId"].ToString().Contains("__"))
                            {
                                exitPathStatus.ExitPathId = (string)action_i["id"] + (string)eachPath["outputId"];
                            }
                            else // Case 1,Case 2...
                            {
                                string caseLabel = eachPath["label"].ToString().Replace(" ", "");
                                exitPathStatus.ExitPathId = (string)action_i["id"] + "__" + caseLabel + "__";
                            }
                            
                            exitPathStatus.IsVisited = false;
                            branchStatus.ExitPaths.Add(exitPathStatus);
                            
                        }

                        branchStatusTable.Add((string)action_i["id"], branchStatus);

                    }


                    switch ((string)action_i["__type"])
                    {
                        case "AskForNLUNextIntentAction":

                            loopStatusTable.Add((string)action_i["id"], new LoopStatus { NextAction = (string)action_i["nextAction"], IsOpen = false });

                            string branchId = (string)action_i["id"] + (string)action_i["outputId"];
                            edges.Add(Tuple.Create((string)action_i["id"], (string)action_i["paths"][0]["nextActionId"]));
                            Logger.Info($"Create edges [LoopAction] Add:[{(string)action_i["trackingId"]}][{(string)action_i["name"]}] {(string)action_i["id"]}=>{(string)action_i["paths"][0]["nextActionId"]}");

                            edges.Add(Tuple.Create((string)action_i["id"], branchId));
                            Logger.Info($"Create edges [LoopAction] Add:[{(string)action_i["trackingId"]}][{(string)action_i["name"]}] {(string)action_i["id"]}=>{branchId}");

                            break;

                        default:
                            Stack<string> stackForOrder = new();
                            foreach (var eachPath in action_i["paths"])
                            {

                                branchId = null;
                                if (eachPath["outputId"].ToString().Contains("__"))
                                {
                                    branchId = (string)action_i["id"] + (string)eachPath["outputId"];
                                }
                                else // Case 1,Case 2...
                                {
                                    string caseLabel = eachPath["label"].ToString().Replace(" ", "");
                                    branchId = (string)action_i["id"] + "__" + caseLabel + "__";
                                }
                                stackForOrder.Push(branchId);

                            }

                            if (stackForOrder.Count > 0)
                            {
                                do
                                {
                                    branchId = stackForOrder.Pop();
                                    edges.Add(Tuple.Create((string)action_i["id"], branchId));
                                    Logger.Info($"Create edges [paths] Add:[{(string)action_i["trackingId"]}][{(string)action_i["name"]}] {(string)action_i["id"]}=>{branchId}");

                                } while (stackForOrder.Count > 0);

                            }

                            break;
                    }

                }

                if ((string)action_i["__type"] == "LoopAction")
                {
                    
                    if ((string)action_i["path"]["nextActionId"]!= null){

                        loopStatusTable.Add((string)action_i["id"], new LoopStatus { NextAction = (string)action_i["nextAction"], IsOpen = false });

                        edges.Add(Tuple.Create((string)action_i["id"], (string)action_i["path"]["nextActionId"]));
                        Logger.Info($"Create edges [LoopAction] Add:[{(string)action_i["trackingId"]}][{(string)action_i["name"]}] {(string)action_i["id"]}=>{(string)action_i["path"]["nextActionId"]}");

                    }

                    string branchId = (string)action_i["id"];
                    edges.Add(Tuple.Create((string)action_i["id"], branchId));
                    Logger.Info($"Create edges [LoopAction] Add:[{(string)action_i["trackingId"]}][{(string)action_i["name"]}] {(string)action_i["id"]}=>{branchId}");

                }

            }

            var graph = new Graph<string>(actionListBranchAdded, edges);
            List<string> visitedForSort = new List<string>();

            foreach (var startId_i in actionListBranchAdded.Where(x => (string)x["trackingId"] == "*S*").Select(y => y["id"]))
            {

                Logger.Info(startId_i);
                visitedForSort.AddRange(DFS(graph, (string)startId_i, actionListBranchAdded, branchStatusTable,loopStatusTable,flowType));

            }

            // Create flowNodeList
            List<GenesysCloudFlowNode> flowNodeList = new();
            foreach (var action_i in actionListBranchAdded)
            {

                GenesysCloudFlowNode flowNode = new();
                flowNode.Name = "(" + action_i["trackingId"] + ")" + action_i["name"].ToString();
                flowNode.Id = action_i["id"].ToString();
                flowNode.Type = action_i["__type"].ToString();

                // [ADD-1]2023/03/25
                if (action_i["taskName"] != null)
                {
                    flowNode.FlowGroup = action_i["taskName"].ToString();

                }
                else
                {
                    string notVisitedSubNodeId = (string)action_i["id"];
                    string[] tokens = notVisitedSubNodeId.Split("__");
                    string notVisitedNodeId = tokens[0];
                    string taskNameFromParentNode = actionListBranchAdded.Where(x => (string)x["id"] == notVisitedNodeId).Select(x => x["taskName"]).FirstOrDefault().ToString();
                    flowNode.FlowGroup = taskNameFromParentNode;
                }

                if (action_i["nextAction"] != null)
                {
                    flowNode.NextAction = action_i["nextAction"].ToString();
                }

                if(flowNode.Type== "AskForNLUNextIntentAction")
                {
                    flowNode.NextAction = action_i["paths"][0]["nextActionId"].ToString();
                }

                if (action_i["parentId"] != null)
                {
                    flowNode.ParentId.Add(action_i["parentId"].ToString());
                }

                // Add node description to flowNode.Desc2
                switch (flowNode.Type)
                {
                    case "TransferPureMatchAction":
                        flowNode.InQueueFlowName = (string)action_i["inQueueFlowName"];
                        flowNode.Queues = action_i["queues"][0]["text"].ToString();

                        JArray skills = (JArray)action_i["skills"];
                        var skillList = skills.Select(x => x["skill"]).Select(x => x["text"]).ToList();
                        flowNode.Skills = String.Join("|", skillList);

                        flowNode.Priority = action_i["priority"]["text"].ToString();

                        flowNode.Desc2 = "Q:" + flowNode.Queues + " S:" + flowNode.Skills;
                        break;

                    case "TransferGroupAction":
                        flowNode.Desc2 = (string)action_i["transferTo"] ?? (string)action_i["group"]["text"];
                        break;

                    case "TransferFlowAction":
                        flowNode.Desc2 = (string)action_i["transferTo"] ?? (string)action_i["flowName"];
                        break;

                    case "TransferExternalAction":
                        flowNode.Desc2 = (string)action_i["transferTo"] ?? (string)action_i["externalNumber"]["text"];
                        break;

                    case "TransferFlowSecureAction":
                        flowNode.Desc2 = (string)action_i["transferTo"] ?? (string)action_i["flowName"];
                        break;

                    case "TransferUserAction":
                        flowNode.Desc2 = (string)action_i["transferTo"] ?? (string)action_i["user"]["text"];
                        break;

                    case "TransferVoicemailAction":
                        string _target = (string)action_i["transferTo"];
                        if (_target != null)
                        {
                            flowNode.Desc2 = _target;
                        }
                        else
                        {
                            flowNode.Desc2 = (string)action_i["transferTarget"]["text"] ?? (string)action_i["transferTargetGroup"]["text"];
                        }

                        break;

                    case "LoopAction":

                        Dictionary<string, string> dict = new()
                        {
                            { "label", action_i["path"]["label"].ToString() },
                            { "ParentId", action_i["id"].ToString() }
                        };

                        if (action_i["path"]["outputId"].ToString().Contains("__"))
                        {
                            dict.Add("subId", flowNode.Id + action_i["path"]["outputId"].ToString());
                        }

                        if (action_i["path"]["nextActionId"] != null)
                        {
                            dict.Add("nextActionId", action_i["path"]["nextActionId"].ToString());
                        }

                        flowNode.Path.Add(dict);

                        if (action_i["nextAction"] != null)
                        {
                            flowNode.NextAction = flowNode.Id + "__LOOP__";
                        }
                        flowNode.Desc2 = "Loop Count:" + action_i["loopCount"]["text"].ToString();

                        break;


                    case "AskForNLUNextIntentAction":

                        dict = new()
                        {
                            { "label", action_i["paths"][0]["label"].ToString() },
                            { "ParentId", action_i["id"].ToString() }
                        };


                        if (action_i["paths"][0]["outputId"].ToString().Contains("__"))
                        {
                            dict.Add("subId", flowNode.Id + action_i["paths"][0]["outputId"].ToString());
                        }

                        if (action_i["paths"][0]["nextActionId"] != null)
                        {
                            dict.Add("nextActionId", action_i["paths"][0]["nextActionId"].ToString());
                        }

                        flowNode.Path.Add(dict);

                        break;

                    case "PlayAudioAction":
                        string audioText= (string)action_i["prompts"]["defaultAudio"]["text"] ?? (string)action_i["prompts"]["defaultAudio"]["text"]; 

                        Regex regExAp = new Regex(@"((?:.+?\()(?!Append)(.+)(,.+))");

                        Match match = regExAp.Match(audioText);
                        if (match.Success)
                        {
                            flowNode.Desc2 = match.Groups[2].Value.ToString();
                        }
                        else
                        {
                            flowNode.Desc2 = audioText;
                        }

                        break;

                    case "DataAction":
                        flowNode.Desc2 = (string)action_i["actionName"] ?? (string)action_i["actionName"];

                        break;

                    case "DecisionAction":
                        flowNode.Desc2 = (string)action_i["expression"]["text"] ?? (string)action_i["expression"]["text"];

                        break;

                    case "DataTableLookupAction":
                        flowNode.Desc2 = (string)action_i["datatableName"] ?? (string)action_i["datatableName"];

                        break;

                    case "UpdateVariableAction":
                        List<JToken> variables = new();
                        if (showExpression)
                        {
                            variables = action_i["variables"].ToList();
                            flowNode.Desc2 = CreateVariableDescriotions(variables, maxSecondDescriptionLengh, flowNode.Type);

                        }
                        break;

                    case "SetAttributesAction":
                        if (showExpression)
                        {
                            variables = action_i["variables"].ToList();
                            flowNode.Desc2 = CreateVariableDescriotions(variables, maxSecondDescriptionLengh, flowNode.Type);
                        }
                        break;

                    case "GetAttributesAction":
                        if (showExpression)
                        {
                            variables = action_i["variables"].ToList();
                            flowNode.Desc2 = CreateVariableDescriotions(variables, maxSecondDescriptionLengh, flowNode.Type);
                        }
                        break;


                    default:

                        break;
                }

                // Branch node
                if (action_i["paths"] != null)
                {
                    if(flowNode.Type!= "AskForNLUNextIntentAction")
                    {
                        foreach (var eachPath in action_i["paths"])
                        {

                            Dictionary<string, string> dict = new()
                            {
                                { "label", eachPath["label"].ToString() },
                                { "ParentId", action_i["id"].ToString() }
                            };

                            if (eachPath["nextActionId"] != null)
                            {
                                dict.Add("nextActionId", eachPath["nextActionId"].ToString());

                            }

                            if (eachPath["outputId"].ToString().Contains("__"))
                            {
                                dict.Add("subId", flowNode.Id + eachPath["outputId"].ToString());

                            }
                            else // Case 1,Case 2...
                            {
                                dict.Add("subId", flowNode.Id + "__" + eachPath["label"].ToString().Replace(" ", "") + "__");

                            }

                            flowNode.Path.Add(dict);

                        }
                    }

                }

                flowNodeList.Add(flowNode);

            }


            // Add parent Id
            foreach (var flowNode_i in flowNodeList)
            {

                if (flowNode_i.ParentId.Count == 0)
                {
                    List<string> parentIdList = QueryParentId(flowNode_i, flowNodeList);
                    flowNode_i.ParentId.AddRange(parentIdList);

                }

            }


            // Numbering to each node
            int index = 0;
            foreach (var id in visitedForSort.Select(x => x).Distinct())
            {

                SetSeqNumber(id, index, flowNodeList);
                index++;

            }

            // Create node list for each branch path. KVP will match at Termination node.
            var hasPathNodeList = flowNodeList.Where(x => x.Path.Count > 0).ToList();

            // Remove each ID from parent ID to remove the line which nextAction connects to termination node.
            foreach (var pathNode_i in hasPathNodeList)
            {

                if (!String.IsNullOrEmpty(pathNode_i.NextAction))
                {
                    foreach (var updateNode_i in flowNodeList)
                    {

                        // To remove the line from Loop start to Loop End.
                        if (updateNode_i.Id == pathNode_i.NextAction && updateNode_i.Type != "AskForNLUNextIntentAction")
                        {
                            updateNode_i.ParentId.Remove(pathNode_i.Id);
                        }

                        // Prevent __LOOP__ node back to normal node as this should be back to LOOP start.
                        var updateNodeParentList = updateNode_i.ParentId.Where(x => x.Contains("__LOOP__")).FirstOrDefault();
                        if (updateNodeParentList != null)
                        {
                            string loopStartId = updateNodeParentList.Substring(0, updateNodeParentList.IndexOf("_"));

                            if (updateNode_i.NextAction == loopStartId)
                            {
                                updateNode_i.ParentId.Remove(updateNodeParentList);

                            }

                        }



                    }

                }

            }

            // Set parentId to just beneath of LoopAction node.
            var loopActionNodeList = flowNodeList.Where(x => x.Type == "LoopAction" || x.Type== "AskForNLUNextIntentAction").Where(y => y.Path.Count > 0);
            foreach (var loopAction_i in loopActionNodeList)
            {

                string nextActionId = loopAction_i.Path.SelectMany(x => x).Where(y => y.Key == "nextActionId").Select(z => z.Value).FirstOrDefault()?.ToString();
                string loopRootId = loopAction_i.Id;

                foreach (var updateNode_i in flowNodeList)
                {

                    if (updateNode_i.Id == nextActionId)
                    {
                        updateNode_i.ParentId.Add(loopRootId);

                    }
                    if (updateNode_i.Id == loopRootId)
                    {
                        // Add line back to Loop start
                        if (!updateNode_i.ParentId.Where(x => x == loopRootId + "__LOOP__").Any())
                        {
                            updateNode_i.ParentId.Add(loopRootId + "__LOOP__");

                        }
                        
                    }

                }
            }

            // Remove line for disconnect or transfer action.
            Regex regEx = new Regex(@"(DisconnectAction|EndFlowAction|EndTaskAction|EndStateAction|ExitBotFlowAction|Transfer.*(?<![_Sub]))");
            var disconnectOrTransferNodeList = flowNodeList.Where(x => regEx.IsMatch(x.Type)).Select(y => y.Id);

            foreach (var removeNode_i in disconnectOrTransferNodeList)
            {

                foreach (var updateNode_i in flowNodeList)
                {

                    if (updateNode_i.ParentId.Contains(removeNode_i) && !updateNode_i.Id.Contains("_") && !removeNode_i.Contains("FAILURE"))
                    {
                        updateNode_i.ParentId.Remove(removeNode_i);

                    }
                    if (updateNode_i.ParentId.Contains(removeNode_i) && updateNode_i.Id.Contains("__LOOP__") && !removeNode_i.Contains("FAILURE"))
                    {
                        updateNode_i.ParentId.Remove(removeNode_i);

                    }

                }
            }

            var stillZeroSeq = flowNodeList.Where(x => x.Seq == 0 && x.Type != "Start");
            if (stillZeroSeq.Any())
            {
                int maxSeq = flowNodeList.Max(x => x.Seq) + 1;
                foreach (var item in stillZeroSeq)
                {

                    SetSeqNumber(item.Id, maxSeq, flowNodeList);
                    maxSeq++;
                }

            }

            List<GenesysCloudFlowNode> FlowNodesListSort = new List<GenesysCloudFlowNode>(flowNodeList.OrderBy(x => x.Seq));

            return FlowNodesListSort;

        }

        /// <summary>
        /// Numbering to each step for CSV records
        /// </summary>
        /// <param name="id"></param>
        /// <param name="seq"></param>
        /// <param name="flowNodeList"></param>
        private static void SetSeqNumber(string id, int seq, List<GenesysCloudFlowNode> flowNodeList)
        {
            foreach (var updateNode_i in flowNodeList)
            {

                // updateNode_i.Seq is 0 by default
                if (updateNode_i.Id == id && updateNode_i.Seq==0)
                {
                    updateNode_i.Seq = seq;
                    break;

                }

            }

        }

        /// <summary>
        /// Set sequence number to nodes under branch nodes
        /// </summary>
        /// <param name="id"></param>
        /// <param name="index"></param>
        /// <param name="flowNodeList"></param>
        /// <param name="pathNode"></param>
        /// <returns></returns>
        private static Tuple<string, int> SetSeqNumberToPathNode(string id, int index, List<GenesysCloudFlowNode> flowNodeList, List<KeyValuePair<string, Dictionary<string, string>>> pathNode)
        {
            string originalId = id;

            // Add success node as TransferPureMatchAction has only failure node.
            if (flowNodeList.Where(x => x.Id == id).Select(y => y.Type).Contains("TransferPureMatchAction"))
            {
                id = originalId + "__SUCCESS__";
                SetSeqNumber(id, index, flowNodeList);
                index++;

            }

            foreach (var pathNode_i in pathNode)
            {

                id = pathNode_i.Key.ToString();
                SetSeqNumber(id, index, flowNodeList);
                index++;

                // If both success and failure path is empty, Set seq number and terminate process so that failure node does not traverse beyond this point.
                bool noPath = flowNodeList.Where(x => x.Id == id).Select(y => y.IsAllWithoutPath).FirstOrDefault();

                if (!noPath)
                {
                    var idKeys = pathNode_i.Value.Select(x => x.Key);
                    foreach (var idKey_i in idKeys)
                    {

                        id = idKey_i.ToString();

                        // To avoid infinity loop if id is the same.
                        if (originalId != id)
                        {
                            SetSeqNumber(id, index, flowNodeList);
                            index++;

                            // If it examine each node in the path, it may contain a branch node, so pick it up here.
                            pathNode = flowNodeList.Where(x => x.Id == id).SelectMany(y => y.PathNode).ToList();
                            if (pathNode.Count > 0)
                            {
                                var result = SetSeqNumberToPathNode(id, index, flowNodeList, pathNode);
                                id = result.Item1;
                                index = result.Item2;

                            }

                        }

                    }

                    // To avoid infinity loop if id is the same.
                    if (originalId == id)
                    {
                        break;

                    }

                    // End of path 1 loop after branch node, examine if end of the path is branch node or not before going to path2.
                    pathNode = flowNodeList.Where(x => x.Id == id).SelectMany(y => y.PathNode).ToList();
                    if (pathNode.Count > 0)
                    {
                        var res = SetSeqNumberToPathNode(id, index, flowNodeList, pathNode);
                        id = res.Item1;
                        index = res.Item2;

                    }

                }

            }

            return new Tuple<string, int>(id, index);

        }

        /// <summary>
        /// Add parentId to each node
        /// </summary>
        /// <param name="flowNode"></param>
        /// <param name="flowNodeList"></param>
        /// <returns></returns>
        private static List<string> QueryParentId(GenesysCloudFlowNode flowNode, List<GenesysCloudFlowNode> flowNodeList)
        {
            return flowNodeList.Where(x => x.NextAction == flowNode.Id).Select(y => y.Id).ToList();

        }

        /// <summary>
        /// Prepare exit path node for path/paths/menuChoiceList(menuOnly)
        /// </summary>
        /// <param name="FlowNodesList"></param>
        /// <returns></returns>
        private static List<JToken> CreateBranchNode(JArray flowSeqItemList, string initialSequence, JArray reusableTaskList,string defaultLanguage)
        {

            List<string> startActionIdList = flowSeqItemList.Where(x => (string)x["startAction"] != null).Select(y => y.Value<string>("id")).ToList();
            var menuChoiceListExist = flowSeqItemList.Select(x => x["menuChoiceList"]).ToList().FirstOrDefault();


            GenesysCloudFlowNode branchNode = new();
            JArray tmpActionList = new();
            string branchId = null;
            string branchNextAction = null;
            string branchName = null;
            string branchType = null;
            string branchTrackingId = null;
            string taskName = null;
            int i = 0;
            List<JToken> response = new();

            // [C]2023/01/17 fixed
            // [A]2023/03/05 fixed
            if (startActionIdList.Count == 0 && menuChoiceListExist==null)
            {
                // Create Main task step
                branchId = "00000000-0000-0000-0000-00000000000" + i.ToString();
                branchNextAction = flowSeqItemList.Where(x => x.Value<string>("id") == initialSequence).Select(y => y.Value<string>("startAction")).FirstOrDefault()?.ToString() ?? initialSequence;
                taskName = flowSeqItemList.Where(x => x.Value<string>("id") == initialSequence).Select(y => y.Value<string>("name")).FirstOrDefault()?.ToString() ?? "Main Task";

                var jvalue = SetJvalue(branchId, "S", "Main Task:" + taskName, "Start", branchNextAction, null);
                tmpActionList.Add(jvalue);


                response = tmpActionList.ToList();

                return response;

            }

            foreach (var id_i in startActionIdList)
            {

                if (id_i == initialSequence)
                {
                    // Create Main task step
                    branchId = "00000000-0000-0000-0000-00000000000" + i.ToString();
                    branchNextAction = flowSeqItemList.Where(x => x.Value<string>("id") == initialSequence).Select(y => y.Value<string>("startAction")).FirstOrDefault()?.ToString() ?? initialSequence;
                    taskName = flowSeqItemList.Where(x => x.Value<string>("id") == initialSequence).Select(y => y.Value<string>("name")).FirstOrDefault()?.ToString() ?? "Main Task";

                    var jvalue = SetJvalue(branchId, "S", "Main Task:"+taskName, "Start", branchNextAction, null);
                    tmpActionList.Add(jvalue);

                }
                else
                {
                    // Create reusable task step
                    branchId = "00000000-0000-0000-0000-00000000000" + i.ToString();
                    branchNextAction = flowSeqItemList.Where(x => x.Value<string>("id") == id_i).Select(y => y.Value<string>("startAction")).FirstOrDefault()?.ToString() ?? id_i;
                    taskName = flowSeqItemList.Where(x => x.Value<string>("id") == id_i).Select(y => y.Value<string>("name")).FirstOrDefault()?.ToString();

                    if (reusableTaskList != null)
                    {
                        branchName = reusableTaskList.Where(x => (string)x["id"] == id_i).Any() ? "Reusable Task" : "Task";

                    }
                    else
                    {
                        branchName = "Task";

                    }

                    var jvalue = SetJvalue(branchId, "S", branchName+":"+taskName, "Start", branchNextAction, null);
                    tmpActionList.Add(jvalue);
                }

                i++;

            }

            foreach (var flowSeqItem in flowSeqItemList)
            {
                // Task flow
                if (flowSeqItem["actionList"] != null)
                {
                    var actionList = flowSeqItem.SelectToken("actionList").ToList();

                    foreach (var action_i in actionList)
                    {

                        tmpActionList.Add(action_i);

                        if ((string)action_i["__type"] == "LoopAction")
                        {
                            branchId = action_i["id"].ToString() + action_i["path"]["outputId"].ToString();
                            branchTrackingId = action_i["trackingId"].ToString();
                            branchName = action_i["name"].ToString() + "_END";
                            branchType = action_i["__type"].ToString() + "_Sub";
                            branchNextAction = (string)action_i["nextAction"];
                            var jvalue = SetJvalue(branchId, branchTrackingId, branchName, branchType, branchNextAction, null);
                            tmpActionList.Add(jvalue);

                        }

                        if (action_i["paths"] != null)
                        {

                            switch ((string)action_i["__type"])
                            {

                                case "AskForNLUNextIntentAction":

                                    branchId = action_i["id"].ToString() + action_i["paths"][0]["outputId"].ToString();
                                    branchTrackingId = action_i["trackingId"].ToString();
                                    branchName = action_i["name"].ToString() + "_END";
                                    branchType = action_i["__type"].ToString() + "_Sub";
                                    branchNextAction = (string)action_i["nextAction"];
                                    var jvalue = SetJvalue(branchId, branchTrackingId, branchName, branchType, branchNextAction, null);
                                    tmpActionList.Add(jvalue);

                                    break;

                                default:

                                    foreach (var paths in action_i["paths"])
                                    {

                                        branchId = null;
                                        branchNextAction = null;
                                        if (paths["outputId"].ToString().Contains("__"))
                                        {
                                            branchId = action_i["id"].ToString() + paths["outputId"].ToString();
                                        }
                                        else // Case 1,Case 2...
                                        {
                                            branchId = action_i["id"].ToString() + "__" + paths["label"].ToString().Replace(" ", "") + "__";

                                        }
                                        branchTrackingId = action_i["trackingId"].ToString();
                                        branchName = action_i["name"].ToString() + "_" + paths["label"].ToString();
                                        branchType = action_i["__type"].ToString() + "_Sub";
                                        if (paths["nextActionId"] != null)
                                        {
                                            branchNextAction = paths["nextActionId"].ToString();
                                        }
                                        else if (action_i["nextAction"] != null)
                                        {
                                            branchNextAction = action_i["nextAction"].ToString();
                                        }

                                        jvalue = SetJvalue(branchId, branchTrackingId, branchName, branchType, branchNextAction, action_i["id"].ToString());
                                        tmpActionList.Add(jvalue);

                                    }

                                    break;
                            }


                            if (action_i["__type"].ToString().Contains("Transfer"))
                            {
                                if (action_i["paths"].Select(x => x["outputId"].ToString()).Where(y => y.Contains("__FAILURE__")).Any())
                                {
                                    branchId = action_i["id"].ToString() + "__SUCCESS__";
                                    branchTrackingId = action_i["trackingId"].ToString();
                                    branchName = action_i["name"].ToString() + "__SUCCESS__";
                                    branchType = action_i["__type"].ToString() + "_Sub";
                                    var jvalue = SetJvalue(branchId, branchTrackingId, branchName, branchType, null, action_i["id"].ToString());
                                    tmpActionList.Add(jvalue);

                                }


                            }

                            if (action_i["__type"].ToString().Contains("ProcessVoicemailInputAction"))
                            {
                                branchId = action_i["id"].ToString() + "__SUCCESS__";
                                branchTrackingId = action_i["trackingId"].ToString();
                                branchName = action_i["name"].ToString() + "__SUCCESS__";
                                branchType = action_i["__type"].ToString() + "_Sub";
                                var jvalue = SetJvalue(branchId, branchTrackingId, branchName, branchType, null, action_i["id"].ToString());
                                tmpActionList.Add(jvalue);

                            }

                            if (action_i["__type"].ToString().Contains("CallBotFlowAction"))
                            {
                                branchId = action_i["id"].ToString() + "__DISCONNECT__";
                                branchTrackingId = action_i["trackingId"].ToString();
                                branchName = action_i["name"].ToString() + "__DISCONNECT__";
                                branchType = action_i["__type"].ToString() + "_Sub";
                                var jvalue = SetJvalue(branchId, branchTrackingId, branchName, branchType, null, action_i["id"].ToString());
                                tmpActionList.Add(jvalue);

                            }


                        }

                    }


                }

                // Basic menu flow
                if (flowSeqItem["menuChoiceList"] != null)
                {
                    // [ADD-1]2023/03/25
                    string guiTaskName = flowSeqItem["name"].ToString();
                    branchId = flowSeqItem["id"].ToString();
                    branchTrackingId = flowSeqItem["trackingId"].ToString();
                    branchName = flowSeqItem["name"].ToString(); ;
                    branchType = flowSeqItem["__type"].ToString();

                    JObject jvalueGuiTopNode = new()
                    {
                        { "id", new JValue(branchId) },
                        { "trackingId", new JValue(branchTrackingId) },
                        { "name", new JValue(branchName) },
                        { "__type", new JValue(branchType) },
                        { "taskName", new JValue(guiTaskName) }

                    };

                    tmpActionList.Add(jvalueGuiTopNode);
                    //tmpActionList.Add(flowSeqItem);//do not delete -v1.1

                    foreach (var menuChoise_i in flowSeqItem["menuChoiceList"])
                    {

                        branchId = menuChoise_i["id"].ToString();
                        //branchTrackingId = flowSeqItem["trackingId"].ToString();
                        branchTrackingId = menuChoise_i["action"]["trackingId"].ToString();
                        branchName = menuChoise_i["name"].ToString(); ;
                        branchType = menuChoise_i["action"]["__type"].ToString();

                        string digit=null;
                        string hasSpeechRecTerms=null;
                        if (menuChoise_i["digit"] != null)
                        {
                            digit = menuChoise_i["digit"].ToString();
                            digit = digit.Replace("10", "*").Replace("11", "#");

                        }
                        else
                        {
                            digit = "-";
                        }

                        if (menuChoise_i["speechRecTerms"] != null)
                        {
                            hasSpeechRecTerms = menuChoise_i["speechRecTerms"][defaultLanguage].ToString();
                            hasSpeechRecTerms = hasSpeechRecTerms.Replace("\"", "").Replace("\r\n", " ");

                        }

                        if (hasSpeechRecTerms == "[]")
                        {
                            branchName = branchName + "_[" + digit + "]";

                        }
                        else
                        {
                            branchName = branchName + "_[" + digit + "]" + hasSpeechRecTerms;

                        }

                        // taskReference or menuReference
                        Regex regEx = new Regex("Reference");
                        string referenceName = menuChoise_i["action"].OfType<JProperty>().Select(x => x.Name).Where(y => regEx.IsMatch(y)).FirstOrDefault()?.ToString();
                        string referenceValue = menuChoise_i["action"].OfType<JProperty>().Where(x => regEx.IsMatch(x.Name)).Select(y => y.Value).FirstOrDefault()?.ToString();
                        branchNextAction = null;
                        if (referenceValue != null)
                        {
                            branchNextAction = flowSeqItemList.Where(x => x.Value<string>("id") == referenceValue).Select(y => y.Value<string>("startAction")).FirstOrDefault()?.ToString() ??
                            flowSeqItemList.Where(x => x.Value<string>("id") == referenceValue).Select(y => y.Value<string>("id")).FirstOrDefault()?.ToString();

                        }
                        else
                        {
                            branchNextAction = menuChoise_i["id"].ToString();

                        }

                        var jvalue = SetJvalue(branchId, branchTrackingId, branchName, branchType, branchNextAction, flowSeqItem["id"].ToString());

                        //[ADD]2023/02/28
                        if (jvalue.Value<String>("trackingId").Contains("*"))
                        {
                            string menuTrackingId = jvalue.Value<String>("trackingId").Replace("*","");
                            jvalue.SelectToken("trackingId").Replace(menuTrackingId);
                        }

                        switch (branchType)
                        {

                            case "TransferPureMatchAction": //Transfer to ACD
                                var queues = menuChoise_i["action"]["queues"];
                                jvalue.Add("queues", queues);
                                var skills = menuChoise_i["action"]["skills"];
                                jvalue.Add("skills", skills);
                                var priority = menuChoise_i["action"]["priority"];
                                jvalue.Add("priority", priority);
                                // Add Success and Failure node
                                AddExitPath(menuChoise_i, tmpActionList);
                                break;

                            case "TransferGroupAction":
                                string transferTo = (string)menuChoise_i["action"]["group"]["text"];
                                jvalue.Add("transferTo", transferTo);
                                AddExitPath(menuChoise_i, tmpActionList);
                                break;

                            case "TransferFlowAction":
                                transferTo = (string)menuChoise_i["action"]["flowName"];
                                jvalue.Add("transferTo", transferTo);
                                AddExitPath(menuChoise_i, tmpActionList);
                                break;

                            case "TransferExternalAction":
                                transferTo = (string)menuChoise_i["action"]["externalNumber"]["text"];
                                jvalue.Add("transferTo", transferTo);
                                AddExitPath(menuChoise_i, tmpActionList);
                                break;

                            case "TransferFlowSecureAction":
                                transferTo = (string)menuChoise_i["action"]["flowName"];
                                jvalue.Add("transferTo", transferTo);
                                AddExitPath(menuChoise_i, tmpActionList);
                                break;

                            case "TransferUserAction":
                                transferTo = (string)menuChoise_i["action"]["user"]["text"];
                                jvalue.Add("transferTo", transferTo);
                                AddExitPath(menuChoise_i, tmpActionList);
                                break;

                            case "TransferVoicemailAction":
                                transferTo = (string)menuChoise_i["action"]["transferTarget"]["text"];
                                if (transferTo != null)
                                {
                                    jvalue.Add("transferTo", transferTo);

                                }
                                else
                                {
                                    transferTo = (string)menuChoise_i["action"]["transferTargetGroup"]["text"];
                                    jvalue.Add("transferTo", transferTo);

                                }

                                AddExitPath(menuChoise_i, tmpActionList);
                                break;

                            default:

                                break;
                        }

                        // [ADD-1]2023/03/25
                        jvalue.Add("taskName", guiTaskName);
                        tmpActionList.Add(jvalue);

                    }

                }

            }

            response = new();
            response = tmpActionList.ToList();

            return response;
        }

        /// <summary>
        /// Add Exit Path to GUI flow
        /// </summary>
        /// <param name="node"></param>
        /// <param name="tmpActionList"></param>
        private static void AddExitPath(JToken node, JArray tmpActionList)
        {
            string branchNameSub, branchTypeSub, branchTrackingIdSub, branchNextAction, parentIdSub, branchIdSub;
            string preTransferAudio = (string)node["action"]["preTransferAudio"]["defaultAudio"]["text"];
            JObject jvalueSub;

            // Add success node as transfer nodes have failure node only.
            if (preTransferAudio.Length > 0)
            {
                branchNameSub = preTransferAudio;
                branchTypeSub = node["action"]["__type"].ToString() + "_Sub";
                branchTrackingIdSub = node["action"]["trackingId"].ToString();

                branchNextAction = node["id"].ToString() + "__SUCCESS__"; ;
                parentIdSub = node["id"].ToString();
                branchIdSub = node["id"].ToString() + "__preTransferAudio__";
                jvalueSub = SetJvalue(branchIdSub, branchTrackingIdSub, branchNameSub, branchTypeSub, branchNextAction, parentIdSub);
                tmpActionList.Add(jvalueSub);

            }

            branchNameSub = node["action"]["name"].ToString() + "_SUCCESS";
            branchTypeSub = node["action"]["__type"].ToString() + "_Sub";
            branchTrackingIdSub = node["action"]["trackingId"].ToString();


            if (preTransferAudio.Length > 0)
            {
                parentIdSub = node["id"].ToString() + "__preTransferAudio__";

            }
            else
            {
                parentIdSub = node["id"].ToString();

            }

            branchIdSub = node["id"].ToString() + "__SUCCESS__";
            jvalueSub = SetJvalue(branchIdSub, branchTrackingIdSub, branchNameSub, branchTypeSub, null, parentIdSub);
            tmpActionList.Add(jvalueSub);

            string failureTransferAudio = node["action"]["failureTransferAudio"]["defaultAudio"]["text"].ToString();
            if (failureTransferAudio.Length > 0)
            {
                branchNameSub = failureTransferAudio;
                branchTypeSub = node["action"]["__type"].ToString() + "_Sub";
                branchTrackingIdSub = node["action"]["trackingId"].ToString();
                branchNextAction = node["id"].ToString() + "__FAILURE__";
                parentIdSub = branchNextAction;
                branchIdSub = node["id"].ToString() + "__failureTransferAudio__";
                jvalueSub = SetJvalue(branchIdSub, branchTrackingIdSub, branchNameSub, branchTypeSub, branchNextAction, parentIdSub);
                tmpActionList.Add(jvalueSub);

            }

            branchNameSub = node["action"]["name"].ToString() + "_FAILURE";
            branchTypeSub = node["action"]["__type"].ToString() + "_Sub";
            branchTrackingIdSub = node["action"]["trackingId"].ToString();
            parentIdSub = node["id"].ToString();
            branchIdSub = node["id"].ToString() + "__FAILURE__";
            jvalueSub = SetJvalue(branchIdSub, branchTrackingIdSub, branchNameSub, branchTypeSub, null, parentIdSub);
            tmpActionList.Add(jvalueSub);

        }



        /// <summary>
        /// Traverse node with DFS
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="graph"></param>
        /// <param name="start"></param>
        /// <param name="actionListBranchAdded"></param>
        /// <param name="branchTable"></param>
        /// <param name="loopTable"></param>
        /// <returns></returns>
        private static List<string> DFS<T>(Graph<T> graph, string start, List<JToken> actionListBranchAdded, Dictionary<string, BranchStatus> branchStatusTable, Dictionary<string, LoopStatus> loopStatusTable,string flowType)
        {
            Stack<string> stack = new();
            HashSet<string> visited = new();
            List<string> visitedForSort = new();
            Dictionary<string,List<string>> branchIdListinLoop = new();
            List<string> branchIdListinLoopClearNodeList = new();
            List<string> branchModeChangedToFalseNotVisitedSubList = new();
            Dictionary<string, List<string>> branchesInLoop = new();
            Dictionary<string, List<string>> nodesInLoop = new();
            List<Dictionary<string, string>> visitedBranches = new();

            stack.Push(start);
            Logger.Info($"DFS Push: {start}");


            // [ADD-1]2023/03/25
            string taskName = GetTokenStringFromALBA(actionListBranchAdded, start, "name");
            if(!String.IsNullOrEmpty(taskName) )
            {
                Regex regTaskName = new Regex(@"(?:Task:)(.+)");
                Match matchOrg = regTaskName.Match(taskName);
                if (matchOrg.Success)
                {
                    GroupCollection group = matchOrg.Groups;
                    taskName = group[1].Value;
                }

                foreach (char c in Path.GetInvalidFileNameChars())
                {
                    taskName = taskName.Replace(c, '_');
                }
                taskName = taskName.Replace(' ', '_');

            }
            else
            {
                taskName = "Unknown";
            }




            while (stack.Count > 0)
            {

                var vertex = stack.Pop();
                Logger.Info($"(vtx:{vertex})DFS Pop: {vertex}");

                if (visited.Contains(vertex))
                {
                    Logger.Info($"Visited contains: {vertex}");
                    continue;

                }

                visited.Add(vertex);
                visitedForSort.Add(vertex);
                Logger.Info($"(vtx:{vertex})DFS visitedForSort Add");
                // [ADD-1]2023/03/25
                SetValueToALBA(actionListBranchAdded, vertex, "taskName", taskName);
                

                // Logged the list of branches that have been visited, with or without nextAction.
                if (branchStatusTable.Any(x => x.Key == vertex) || loopStatusTable.Any(x => x.Key == vertex))
                {
                    string nextAction = GetTokenStringFromALBA(actionListBranchAdded, vertex, "nextAction");

                    if (!visitedBranches.Any(x => x.ContainsKey(vertex)))
                    {
                        visitedBranches.Add(new Dictionary<string, string> { { vertex, nextAction } });

                    }

                }

                // Update branchStatus table
                if (branchStatusTable.Any(x => x.Key == vertex))
                {
                    int maxBranchOpenSeq = branchStatusTable.Select(x => x.Value.IsOpenSeq).Max();
                    if (branchStatusTable.Where(x => x.Key == vertex).Select(y => y.Value).FirstOrDefault().IsOpenSeq == 0)
                    {
                        branchStatusTable.Where(x => x.Key == vertex).Select(y => y.Value).FirstOrDefault().IsOpenSeq = maxBranchOpenSeq + 1;

                    }

                }

                if (branchStatusTable.SelectMany(x => x.Value.ExitPaths).Where(y => y.ExitPathId == vertex).Any())
                {
                    int maxExitBranchOpenSeq=branchStatusTable.SelectMany(x => x.Value.ExitPaths).Select(y => y.IsExitPathOpenSeq).Max();
                    if (branchStatusTable.SelectMany(x=>x.Value.ExitPaths).Where(y=>y.ExitPathId==vertex).FirstOrDefault().IsExitPathOpenSeq == 0)
                    {
                        branchStatusTable.SelectMany(x => x.Value.ExitPaths).Where(y => y.ExitPathId == vertex).FirstOrDefault().IsExitPathOpenSeq = maxExitBranchOpenSeq + 1;
                    }
                    branchStatusTable.SelectMany(x => x.Value.ExitPaths).Where(y => y.ExitPathId == vertex).FirstOrDefault().IsVisited= true;
                    branchStatusTable.Where(x => x.Key == vertex.Substring(0, vertex.IndexOf("_"))).Select(y => y.Value).FirstOrDefault().IsBranchOpen = true;

                    int ExitPathVisitedCount = branchStatusTable.Where(x => x.Key == vertex.Substring(0, vertex.IndexOf("_"))).Select(y => y.Value).FirstOrDefault().ExitPathVisitedCount;
                    int ExitPathTotalCount = branchStatusTable.Where(x => x.Key == vertex.Substring(0, vertex.IndexOf("_"))).Select(y => y.Value).FirstOrDefault().ExitPathTotalCount;

                    ExitPathVisitedCount = ExitPathVisitedCount+1;
                    branchStatusTable.Where(x => x.Key == vertex.Substring(0, vertex.IndexOf("_"))).Select(y => y.Value).FirstOrDefault().ExitPathVisitedCount= ExitPathVisitedCount;
                    if(ExitPathTotalCount==ExitPathVisitedCount) branchStatusTable.Where(x => x.Key == vertex.Substring(0, vertex.IndexOf("_"))).Select(y => y.Value).FirstOrDefault().AllProcessed= true;


                    // Add branchNodeId if tracing in loopNode
                    List<string> currentOpenLoopIds =loopStatusTable.Where(x=>x.Value.IsOpen).Select(x=>x.Key).ToList();
                    if(currentOpenLoopIds.Count != 0)
                    {
                        foreach (var currentOpenLoopIds_i in currentOpenLoopIds)
                        {

                            if (branchesInLoop.ContainsKey(currentOpenLoopIds_i))
                            {
                                branchesInLoop[currentOpenLoopIds_i].Add(vertex);

                            }
                            else
                            {
                                branchesInLoop.Add(currentOpenLoopIds_i, new List<string> { vertex });

                            }
                        }


                    }


                }


                string nextActionOfCurrentVertex = GetTokenStringFromALBA(actionListBranchAdded, vertex, "nextAction");

                if (loopStatusTable.Where(x => x.Key == vertex).Any())
                {
                    loopStatusTable.Where(x => x.Key == vertex).Select(y => y.Value).FirstOrDefault().IsOpen = true;
                    int maxLoopOpenSeq = loopStatusTable.Select(x => x.Value.IsOpenSeq).Max();
                    if (loopStatusTable.Where(x => x.Key == vertex).Select(y => y.Value).FirstOrDefault().IsOpenSeq == 0)
                    {
                        loopStatusTable.Where(x => x.Key == vertex).Select(y => y.Value).FirstOrDefault().IsOpenSeq = maxLoopOpenSeq + 1;

                    }

                    // LoopId doesn't have nextAction means there is a loopNode under branch node.
                    if (nextActionOfCurrentVertex == null)
                    {
                        Logger.Info("hit");
                        var previousVisitedBranch = visitedBranches.SelectMany(x => x.Keys).Where(x=>x.Equals(vertex)==false).LastOrDefault();

                        foreach (var visitedBranches_i in Enumerable.Reverse(visitedBranches).ToList())
                        {

                            if (!visitedBranches_i.ContainsKey(vertex) && visitedBranches_i.Any(x=>x.Value!=null))
                            {
                                string nextActionOfVisitedBranch = visitedBranches_i.Select(x => x.Value).FirstOrDefault().ToString();
                                // Set nextAction and exit foreach loop if nextAction is not visited yet
                                if (vertex != GetTokenStringFromALBA(actionListBranchAdded, nextActionOfVisitedBranch, "nextAction") && !visited.Any(x=>x.Contains(nextActionOfVisitedBranch)))
                                {
                                    loopStatusTable.Where(x => x.Key == vertex).FirstOrDefault().Value.AssignedNextAction = visitedBranches_i.Select(x => x.Value).FirstOrDefault().ToString();
                                    break;

                                }

                            }

                        }

                        // Still no nextAction in loopNode?
                        if(String.IsNullOrEmpty(loopStatusTable.Where(x => x.Key == vertex).FirstOrDefault().Value.AssignedNextAction))
                        {
                            // Go backward in visitedForSort and collect two branches with nextAction
                            Dictionary<string, int> hasNextActionVisitedBranches = new();
                            foreach (var visitedForSort_i in Enumerable.Reverse(visitedForSort).ToList())
                            {

                                if (visitedForSort_i.Contains("__") && !String.IsNullOrEmpty(branchStatusTable.Where(x=>x.Key==RemoveUnderScoreOfSubNode(visitedForSort_i)).FirstOrDefault().Value.NextAction))
                                {
                                    int posOfbranch = visitedForSort.IndexOf(RemoveUnderScoreOfSubNode(visitedForSort_i));
                                    if (!hasNextActionVisitedBranches.ContainsKey(RemoveUnderScoreOfSubNode(visitedForSort_i)))
                                    {
                                        hasNextActionVisitedBranches.Add(RemoveUnderScoreOfSubNode(visitedForSort_i), posOfbranch);
                                    }

                                }

                                if (hasNextActionVisitedBranches.Count == 2) break;

                            }

                            // Of the two collected branches, set the nextAction of the one on the top of the flow to the loopNode
                            if (hasNextActionVisitedBranches.Count == 2)
                            {
                                string nextActionInFirstHasNextActionVisitedBranches = branchStatusTable.Where(x => x.Key == hasNextActionVisitedBranches.FirstOrDefault().Key).FirstOrDefault().Value.NextAction;
                                string nextActionInLastHasNextActionVisitedBranches = branchStatusTable.Where(x => x.Key == hasNextActionVisitedBranches.LastOrDefault().Key).FirstOrDefault().Value.NextAction;
                                int posOfFirst = visitedForSort.IndexOf(nextActionInFirstHasNextActionVisitedBranches);
                                int posOfLast = visitedForSort.IndexOf(nextActionInLastHasNextActionVisitedBranches);

                                // Use the nextAction that was last found in branchNode
                                if (posOfLast < posOfFirst)
                                {
                                    loopStatusTable.Where(x => x.Key == vertex).FirstOrDefault().Value.AssignedNextAction = nextActionInLastHasNextActionVisitedBranches;

                                }

                            }

                        }

                        var canCloseNode = branchStatusTable.Where(x => x.Value.IsBranchOpen == true && x.Value.AllProcessed == false).OrderByDescending(x=>x.Value.IsOpenSeq).FirstOrDefault().Key;
                        loopStatusTable.Where(x=>x.Key==vertex).FirstOrDefault().Value.CanCloseNode = canCloseNode;

                    }

                }


                if (graph.AdjacencyList[vertex].Count == 0)
                {

                    // Go back through the list of branches that have been visited, and Set nextAction if there it is. If there is a loopNode on the way back, set loopEND to nextAction
                    if (String.IsNullOrEmpty(GetTokenStringFromALBA(actionListBranchAdded, vertex, "nextAction")))
                    {
                        int loopPos = 0;
                        int branchPos = 0;
                        int pos = 0;
                        string loopNodeId = null;
                        string nextActionOfbranchNode = null;
                        string branchNodeId = null;
                        Stack<string> reverseTraced = new();

                        string upperLoopNodeId = null;
                        string upperBranchNodeId = null;
                        string nextActionOfUpperBranchNodeId = null;

                        string firstFoundBranchNodeId = null;
                        int firstFoundBranchNodeIdPosInVisitedBranches = 0;

                        // [C]2023/01/19 fixed
                        bool preventConnectToLoopEnd = false;

                        foreach (var visitedNodes_i in Enumerable.Reverse(visitedForSort).ToList())
                        {

                            string tmp_visitedNodes_i = visitedNodes_i;
                            // __LOOP__ node is not necessary on the way back
                            if (!visitedNodes_i.Contains("__LOOP__"))
                            {
                                tmp_visitedNodes_i = RemoveUnderScoreOfSubNode(visitedNodes_i);

                            }

                            // Trace visitedBranch list from the first found branchNode and set loopend of the first found loopNode
                            if (branchStatusTable.Any(x => x.Key == tmp_visitedNodes_i) && String.IsNullOrEmpty(firstFoundBranchNodeId) && firstFoundBranchNodeIdPosInVisitedBranches == 0)
                            {
                                firstFoundBranchNodeId = visitedNodes_i;
                                firstFoundBranchNodeIdPosInVisitedBranches = visitedBranches.SelectMany(x => x.Keys).ToList().IndexOf(tmp_visitedNodes_i);

                                List<string> visitedBranchesKey = new();
                                visitedBranchesKey.AddRange(visitedBranches.SelectMany(x=>x.Keys).ToList());

                                if (visitedBranchesKey.LastOrDefault() != tmp_visitedNodes_i)
                                {
                                    int firstFoundBranchPos = visitedBranchesKey.IndexOf(tmp_visitedNodes_i);
                                    visitedBranchesKey.RemoveRange(firstFoundBranchNodeIdPosInVisitedBranches+1, visitedBranchesKey.Count-firstFoundBranchNodeIdPosInVisitedBranches-1);

                                }


                                foreach (var visitedBranchesKey_i in Enumerable.Reverse(visitedBranchesKey))
                                {

                                    if (loopStatusTable.ContainsKey(visitedBranchesKey_i.ToString())&&String.IsNullOrEmpty(upperLoopNodeId)) 
                                    {
                                        upperLoopNodeId = visitedBranchesKey_i;

                                    }

 
                                    if(branchStatusTable.ContainsKey(visitedBranchesKey_i)&&!String.IsNullOrEmpty(branchStatusTable.Where(x => x.Key == RemoveUnderScoreOfSubNode(visitedBranchesKey_i)).FirstOrDefault().Value.NextAction))
                                    {
                                        string tmp_NextAction = branchStatusTable.Where(x => x.Key == RemoveUnderScoreOfSubNode(visitedBranchesKey_i)).FirstOrDefault().Value.NextAction;
                                        if(branchStatusTable.Where(x=>x.Key==tmp_NextAction).Any() && String.IsNullOrEmpty(branchStatusTable.Where(x => x.Key == RemoveUnderScoreOfSubNode(tmp_NextAction)).FirstOrDefault().Value.NextAction) ||tmp_NextAction==vertex)
                                        {
                                            Logger.Info("skipped 01");

                                        }
                                        else
                                        {
                                            if (String.IsNullOrEmpty(upperBranchNodeId))
                                            {
                                                upperBranchNodeId = visitedBranchesKey_i;
                                                nextActionOfUpperBranchNodeId = tmp_NextAction;

                                            }

                                        }

                                    }
                                    if(!String.IsNullOrEmpty(upperLoopNodeId)&&!String.IsNullOrEmpty(upperBranchNodeId))
                                    {
                                        break;

                                    }

                                }

                            }


                            if (loopStatusTable.Any(x => x.Key == tmp_visitedNodes_i) && String.IsNullOrEmpty(loopNodeId))
                            {
                                loopPos = pos;
                                loopNodeId = loopStatusTable.Where(x => x.Value.IsOpen == true).OrderByDescending(x => x.Value.IsOpenSeq).FirstOrDefault().Key;

                            }


                            if (branchStatusTable.Any(x => x.Key == tmp_visitedNodes_i) && !String.IsNullOrEmpty(branchStatusTable.Where(x => x.Key == tmp_visitedNodes_i).FirstOrDefault().Value.NextAction) && String.IsNullOrEmpty(branchNodeId))
                            {
                                // There is caseNode without nextAction on the way back, avoid using nextAction of branchNode under the sibling case branch.
                                bool IsvisitedCaseNode = false;
                                string visitedCaseBranch = reverseTraced.Where(x => x.Contains("__Case")).FirstOrDefault()?.ToString();
                                if(!String.IsNullOrEmpty(visitedCaseBranch)&& String.IsNullOrEmpty(branchStatusTable.Where(x => x.Key == RemoveUnderScoreOfSubNode(visitedCaseBranch)).FirstOrDefault().Value.NextAction))
                                {
                                    // Skip if there is branchNode with nextAction under caseNode without nextAction. If there is a branchNode with nextAction on top of a caseNode without nextAction,I want to use it.
                                    int posOfnoNextActionCaseNode = visitedForSort.IndexOf(RemoveUnderScoreOfSubNode(visitedCaseBranch));
                                    int posOfhasNextActionBranchNode = visitedForSort.IndexOf(RemoveUnderScoreOfSubNode(tmp_visitedNodes_i));
                                    if (posOfhasNextActionBranchNode > posOfnoNextActionCaseNode)
                                    {
                                        IsvisitedCaseNode = true;

                                    }

                                }

                                bool ShouldSkipBranch = false;
                                // Now it traced back to the branchNode with nextAction, but if this nextAction is already on the way back, skip it.
                                nextActionOfbranchNode = branchStatusTable.Where(x => x.Key == tmp_visitedNodes_i).FirstOrDefault().Value.NextAction;
                                if (!String.IsNullOrEmpty(nextActionOfbranchNode)&&!reverseTraced.Contains(nextActionOfbranchNode) && !IsvisitedCaseNode)
                                {
                                    // The branchNode without nextAction was already closed, although it was not on the way back. Skip it and go back to the top of loopNode.
                                    var branchesInReverseTraced = reverseTraced.Where(x => x.Contains("__")).ToList();
                                    foreach (var branchesInReverseTraced_i in branchesInReverseTraced)
                                    {

                                        int posOfReversedBranch = visitedForSort.IndexOf(RemoveUnderScoreOfSubNode(branchesInReverseTraced_i));
                                        int posOfhasNextActionBranchNode = visitedForSort.IndexOf(RemoveUnderScoreOfSubNode(tmp_visitedNodes_i));

                                        if (posOfReversedBranch < posOfhasNextActionBranchNode)
                                        {
                                            ShouldSkipBranch = true;
                                            break;

                                        }
                                    }

                                    if (!ShouldSkipBranch)
                                    {
                                        branchPos = pos;
                                        branchNodeId = tmp_visitedNodes_i;

                                    }

                                }

                                // Flow ends with HOLD step
                                if (nextActionOfbranchNode == vertex)
                                {
                                    break;
                                }


                            }

                            if (!String.IsNullOrEmpty(loopNodeId) && !String.IsNullOrEmpty(branchNodeId))
                            {
                                break;
                            }

                            pos++;
                            reverseTraced.Push(visitedNodes_i);

                        }

                        // LoopNode was found first on the way back.
                        if (loopPos<branchPos &&!String.IsNullOrEmpty(loopNodeId)&& String.IsNullOrEmpty(GetTokenStringFromALBA(actionListBranchAdded, vertex, "nextAction")))
                        {
                            if (GetTokenStringFromALBA(actionListBranchAdded, loopNodeId + "__LOOP__", "nextAction") != vertex && loopNodeId==upperLoopNodeId)
                            {
                                SetValueToALBA(actionListBranchAdded, vertex, "nextAction", upperLoopNodeId + "__LOOP__");
                                Logger.Info("Set 01");

                            }
                            else
                            {
                                SetValueToALBA(actionListBranchAdded, vertex, "nextAction", nextActionOfbranchNode);
                                Logger.Info("Set 02");


                            }

                        }

                        if (nextActionOfbranchNode == RemoveUnderScoreOfSubNode(vertex) && !String.IsNullOrEmpty(loopNodeId) && String.IsNullOrEmpty(GetTokenStringFromALBA(actionListBranchAdded, vertex, "nextAction")))
                        {
                            // Go to loopEnd if branchNode ends only YES path.
                            SetValueToALBA(actionListBranchAdded, vertex, "nextAction", loopNodeId + "__LOOP__");
                            Logger.Info("Set 03");


                        }

                        if (!String.IsNullOrEmpty(nextActionOfbranchNode) && !String.IsNullOrEmpty(branchNodeId))
                        {
                            int openSeqNextActionOfbranchNode = branchStatusTable.Where(x => x.Key == nextActionOfbranchNode).Select(x => x.Value.IsOpenSeq).FirstOrDefault();
                            int openSeqOfbranchNode = branchStatusTable.Where(x => x.Key == branchNodeId).Select(x => x.Value.IsOpenSeq).FirstOrDefault();
                            if (openSeqNextActionOfbranchNode > openSeqOfbranchNode && !String.IsNullOrEmpty(loopNodeId) && String.IsNullOrEmpty(GetTokenStringFromALBA(actionListBranchAdded, vertex, "nextAction")))
                            {
                                // BranchNode with nextAction comes first after loopNode, but the destination node is without nextAction, then change to loopEND.
                                if(String.IsNullOrEmpty(branchStatusTable.Where(x => x.Key == nextActionOfbranchNode).Select(x => x.Value.NextAction).ToString()))
                                {
                                    // Not used
                                    SetValueToALBA(actionListBranchAdded, vertex, "nextAction", loopNodeId + "__LOOP__");
                                    Logger.Info("Set 04");


                                }


                            }

                            if (openSeqNextActionOfbranchNode > openSeqOfbranchNode && !String.IsNullOrEmpty(nextActionOfbranchNode) && String.IsNullOrEmpty(GetTokenStringFromALBA(actionListBranchAdded, vertex, "nextAction")))
                            {
                                SetValueToALBA(actionListBranchAdded, vertex, "nextAction", nextActionOfbranchNode);
                                Logger.Info("Set 05");


                            }

                        }


                        // LoopNode only or there is branchNode without nextAction in loop.
                        if (!String.IsNullOrEmpty(loopNodeId) && String.IsNullOrEmpty(branchNodeId) && String.IsNullOrEmpty(GetTokenStringFromALBA(actionListBranchAdded, vertex, "nextAction")))
                        {

                            foreach (var loopStatusTable_i in loopStatusTable)
                            {

                                var na = loopStatusTable_i.Value.NextAction;
                                var assignedna = loopStatusTable_i.Value.AssignedNextAction;

                                if(na==vertex || assignedna == vertex)
                                {
                                    Logger.Info("skipped 02");

                                }
                                else
                                {
                                    if (loopNodeId == upperLoopNodeId)
                                    {
                                        // [C]2023/01/19 fixed
                                        bool IsNaOfloopIdVisited = false;

                                        if((!String.IsNullOrEmpty(na) && reverseTraced.Any(x => x.Contains(na))) || (!String.IsNullOrEmpty(assignedna) && reverseTraced.Any(x => x.Contains(assignedna))))
                                        {
                                            IsNaOfloopIdVisited = true;
                                        }


                                        if (flowType == "inqueuecall" && vertex.Contains("__") && loopStatusTable.Any(x => x.Key == upperLoopNodeId && x.Value.IsOpen == true) && IsNaOfloopIdVisited)
                                        {
                                            Logger.Info("skipped 03");
                                            preventConnectToLoopEnd=true;

                                        }
                                        else
                                        {

                                            SetValueToALBA(actionListBranchAdded, vertex, "nextAction", loopNodeId + "__LOOP__");
                                            Logger.Info("Set 06");

                                        }






                                    }
                                    else
                                    {
                                        //0912
                                        if (!String.IsNullOrEmpty(upperLoopNodeId) && loopStatusTable.Where(x => x.Key == upperLoopNodeId).Select(x => x.Value).Select(x => x.IsOpen).FirstOrDefault())
                                        {


                                                SetValueToALBA(actionListBranchAdded, vertex, "nextAction", upperLoopNodeId + "__LOOP__");
                                                Logger.Info("Set 07");



                                        }
                                        else if (!String.IsNullOrEmpty(loopNodeId))
                                        {
                                            var targetNode = GetTokenStringFromALBA(actionListBranchAdded, loopNodeId + "__LOOP__", "nextAction");
                                            if (targetNode == vertex)
                                            {
                                                Logger.Info("loopNodeId na points me");
                                                int openSeq = loopStatusTable.Where(x => x.Key == loopNodeId).Select(x => x.Value.IsOpenSeq).FirstOrDefault();
                                                if (openSeq > 0)
                                                {
                                                    int targetOpenSeq = openSeq - 1;
                                                    string targetLoopEnd = loopStatusTable.Where(x => x.Value.IsOpenSeq == targetOpenSeq).Select(x => x.Key).FirstOrDefault();
                                                    SetValueToALBA(actionListBranchAdded, vertex, "nextAction", targetLoopEnd + "__LOOP__");
                                                    Logger.Info("Set 08");

                                                }

                                            }
                                            else
                                            {
                                                SetValueToALBA(actionListBranchAdded, vertex, "nextAction", loopNodeId + "__LOOP__");
                                                Logger.Info("Set 09");

                                            }


                                        }


                                    }


                                }

                            }


                        }

                        if (!String.IsNullOrEmpty(loopNodeId) && !String.IsNullOrEmpty(branchNodeId) && String.IsNullOrEmpty(GetTokenStringFromALBA(actionListBranchAdded, vertex, "nextAction")))
                        {
                            SetValueToALBA(actionListBranchAdded, vertex, "nextAction", nextActionOfbranchNode);
                            Logger.Info("Set 10");

                        }

                        if (String.IsNullOrEmpty(loopNodeId) && !String.IsNullOrEmpty(branchNodeId) && String.IsNullOrEmpty(GetTokenStringFromALBA(actionListBranchAdded, vertex, "nextAction")))
                        {
                            SetValueToALBA(actionListBranchAdded, vertex, "nextAction", nextActionOfbranchNode);
                            Logger.Info("Set 11");

                        }


                        if (String.IsNullOrEmpty(GetTokenStringFromALBA(actionListBranchAdded, vertex, "nextAction")))
                        {
                            if (!String.IsNullOrEmpty(upperLoopNodeId) && loopStatusTable.Where(x => x.Key == upperLoopNodeId).Select(x => x.Value).Select(x => x.IsOpen).FirstOrDefault())
                            {
                                // [C]2023/01/19 fixed
                                if (flowType== "inqueuecall" && vertex.Contains("__") && loopStatusTable.Any(x => x.Key == upperLoopNodeId &&x.Value.IsOpen==false) || preventConnectToLoopEnd)
                                {
                                    Logger.Info("skipped 03");


                                }
                                else
                                {
     
                                    SetValueToALBA(actionListBranchAdded, vertex, "nextAction", upperLoopNodeId + "__LOOP__");
                                    Logger.Info("Set 12");

                                }

                            }
                            else
                            {
                                //0912
                                var secondUpperLoopNodeId = loopStatusTable.Where(x => x.Value.IsOpen == true).OrderBy(x => x.Value.IsOpenSeq).LastOrDefault().Key;
                                SetValueToALBA(actionListBranchAdded, vertex, "nextAction", secondUpperLoopNodeId + "__LOOP__");
                                Logger.Info("Set 13");


                            }

                           

                        }



                    }

                    if (GetTokenStringFromALBA(actionListBranchAdded, vertex, "__type") == "ExitLoopAction")
                    {
                        SetValueToALBA(actionListBranchAdded, vertex, "nextAction", loopStatusTable.Where(x => x.Value.IsOpen == true).OrderByDescending(x => x.Value.IsOpenSeq).FirstOrDefault().Key + "__LOOP__");
                        Logger.Info("Set 13");

                    }

                    if (String.IsNullOrEmpty(GetTokenStringFromALBA(actionListBranchAdded, vertex, "nextAction")))
                    {
                        Logger.Info("No nextAction!!!");
                    }


                }

                // Close loopNode
                if (loopStatusTable.Any(x => x.Value.IsOpen == true))
                {
                    // Set vertex to loopNode if a loopMode is open state.
                    List<string> currentOpenLoopIds = loopStatusTable.Where(x => x.Value.IsOpen).Select(x => x.Key).ToList();

                    foreach (var currentOpenLoopIds_i in currentOpenLoopIds)
                    {

                        if (nodesInLoop.ContainsKey(currentOpenLoopIds_i))
                        {
                            nodesInLoop[currentOpenLoopIds_i].Add(vertex);
                        }
                        else
                        {
                            nodesInLoop.Add(currentOpenLoopIds_i, new List<string> { vertex });
                        }
                    }

                    string currentVertex = RemoveUnderScoreOfSubNode(vertex);

                    // Current vertex is nextAction of a loopNode. can be closed?
                    if (loopStatusTable.Any(x => x.Value.NextAction == currentVertex||x.Value.AssignedNextAction==currentVertex||x.Value.CanCloseNode==currentVertex))
                    {
                        string currentLoopId = loopStatusTable.Where(x => x.Value.IsOpen == true).OrderByDescending(x => x.Value.IsOpenSeq).FirstOrDefault().Key;

                        bool isVisited = false;

                        isVisited = IsAllBranchesProcessed(branchStatusTable, loopStatusTable, branchesInLoop, visited, currentLoopId, currentVertex);

                        if (isVisited)
                        {
                            string nextActionOfcurrentLoopNode;
                            nextActionOfcurrentLoopNode = loopStatusTable.Where(x => x.Key == currentLoopId).FirstOrDefault().Value.NextAction;
                            if (String.IsNullOrEmpty(nextActionOfcurrentLoopNode))
                            {
                                nextActionOfcurrentLoopNode = loopStatusTable.Where(x => x.Key == currentLoopId).FirstOrDefault().Value.AssignedNextAction;

                            }

                            List<string> nextActionOfpreviousLoopNodes = loopStatusTable.Where(x => x.Value.IsOpen == true && x.Value.IsOpenSeq >= 1 && x.Key != currentLoopId).OrderByDescending(x => x.Value.IsOpenSeq).Select(x => x.Key).ToList();
                            if (nextActionOfpreviousLoopNodes.Count == 0)
                            {
                                Logger.Info("Close single loopNode");
                                SetValueToALBA(actionListBranchAdded, currentLoopId + "__LOOP__", "nextAction", nextActionOfcurrentLoopNode);
                                loopStatusTable.Where(x => x.Key == currentLoopId).FirstOrDefault().Value.IsOpenSeq = -1;
                                loopStatusTable.Where(x => x.Key == currentLoopId).FirstOrDefault().Value.IsOpen = false;

                                Logger.Info("Set 14");
                            }
                            else
                            {
                                Logger.Info("Close nested loopNode");
                                foreach (var nextActionOfpreviousLoopNodes_i in nextActionOfpreviousLoopNodes)
                                {

                                    string nextActionOfpreviousLoopNode = loopStatusTable.Where(x => x.Key == nextActionOfpreviousLoopNodes_i).FirstOrDefault().Value.NextAction;
                                    if (string.IsNullOrEmpty(nextActionOfpreviousLoopNode))
                                    {
                                        nextActionOfpreviousLoopNode = loopStatusTable.Where(x => x.Key == nextActionOfpreviousLoopNodes_i).FirstOrDefault().Value.AssignedNextAction;

                                    }

                                    if (nextActionOfpreviousLoopNode == nextActionOfcurrentLoopNode)
                                    {
                                        SetValueToALBA(actionListBranchAdded, currentLoopId + "__LOOP__", "nextAction", nextActionOfpreviousLoopNodes_i + "__LOOP__");
                                        loopStatusTable.Where(x => x.Key == currentLoopId).FirstOrDefault().Value.IsOpenSeq = -1;
                                        loopStatusTable.Where(x => x.Key == currentLoopId).FirstOrDefault().Value.IsOpen = false;

                                        Logger.Info("Set 15");

                                        break;

                                    }
                                    else
                                    {
                                        var subNodesInLoop = branchesInLoop.Where(x => x.Key == currentLoopId).SelectMany(x => x.Value).ToList();

                                        bool isbranchAllProcessed = false;
                                        foreach (var subNodesInLoop_i in subNodesInLoop)
                                        {

                                            string branchNode = RemoveUnderScoreOfSubNode(subNodesInLoop_i);
                                            isbranchAllProcessed = branchStatusTable.Where(x => x.Key == branchNode).Select(x => x.Value.AllProcessed).FirstOrDefault();

                                        }

                                        if (isbranchAllProcessed)
                                        {
                                            nextActionOfpreviousLoopNode = loopStatusTable.Where(x => x.Value.NextAction == nextActionOfcurrentLoopNode || x.Value.AssignedNextAction == nextActionOfcurrentLoopNode).Select(x => x.Key).FirstOrDefault();

                                            if(vertex == nextActionOfcurrentLoopNode && currentLoopId==nextActionOfpreviousLoopNode)
                                            {
                                                SetValueToALBA(actionListBranchAdded, currentLoopId + "__LOOP__", "nextAction", nextActionOfcurrentLoopNode);
                                                loopStatusTable.Where(x => x.Key == currentLoopId).FirstOrDefault().Value.IsOpenSeq = -1;
                                                loopStatusTable.Where(x => x.Key == currentLoopId).FirstOrDefault().Value.IsOpen = false;
                                                Logger.Info("Set 16");

                                            }

                                            if (vertex != nextActionOfcurrentLoopNode && currentLoopId == nextActionOfpreviousLoopNode)
                                            {
                                                SetValueToALBA(actionListBranchAdded, currentLoopId + "__LOOP__", "nextAction", nextActionOfcurrentLoopNode);
                                                loopStatusTable.Where(x => x.Key == currentLoopId).FirstOrDefault().Value.IsOpenSeq = -1;
                                                loopStatusTable.Where(x => x.Key == currentLoopId).FirstOrDefault().Value.IsOpen = false;
                                                Logger.Info("Set 17");

                                            }

                                        }
                                        else
                                        {
                                            Logger.Info("All branches in loop node were not processed yet.");

                                        }


                                    }


                                }


                            }

                        }

                    }


                }

                // Analyze neighbor node 
                foreach (var neighbor_i in graph.AdjacencyList[vertex])
                {

                    Logger.Info($"(nbr:{neighbor_i})Push neighbor to stack started with vertex: {vertex}");

                    if (!visited.Contains(neighbor_i))
                    {
                        stack.Push(neighbor_i);

                        var _stackNeighbor = actionListBranchAdded.Where(x => (string)x["id"] == neighbor_i).Select(x => x).FirstOrDefault();
                        var _stackTrackingId = _stackNeighbor["trackingId"];
                        var _stackNeighbotId = _stackNeighbor["id"];
                        var _stackName = _stackNeighbor["name"];
                        Logger.Info($"(nbr:{neighbor_i})Push neighbor to stack: [{_stackTrackingId}][{_stackName}] {_stackNeighbotId} with vertex: {vertex} ");

                    }

                }
            }

            // All nodes were processed but if there is still open loopNode,set nextAction to LOOPEND if the nextAction has already been visited.
            if (loopStatusTable.Any(x => x.Value.IsOpen == true))
            {

                foreach (var loopStatusTable_i in loopStatusTable.Where(x=>x.Value.IsOpen==true))
                {

                    if (visitedForSort.Contains(loopStatusTable_i.Value.AssignedNextAction) && String.IsNullOrEmpty(GetTokenStringFromALBA(actionListBranchAdded, loopStatusTable_i.Key + "__LOOP__", "nextAction")))
                    {
                        // In some cases, nested loopNodes can not be closed because the final disconnect step is reached first.
                        string previousLoopNodeId = nodesInLoop.Where(x=>x.Key!=loopStatusTable_i.Key && x.Value.Contains(loopStatusTable_i.Key)).Select(x => x.Key).FirstOrDefault();
                        string nextActionOfpreviousLoopNodeId = null;
                        if (!String.IsNullOrEmpty(previousLoopNodeId)) 
                        {
                            nextActionOfpreviousLoopNodeId = loopStatusTable.Where(x => x.Key == previousLoopNodeId).Select(x => x.Value.NextAction).FirstOrDefault();
                        }
                        if(nextActionOfpreviousLoopNodeId == loopStatusTable_i.Value.AssignedNextAction)
                        {
                            SetValueToALBA(actionListBranchAdded, loopStatusTable_i.Key + "__LOOP__", "nextAction", previousLoopNodeId+"__LOOP__");
                            Logger.Info("Set 18");

                        }
                        else
                        {
                            SetValueToALBA(actionListBranchAdded, loopStatusTable_i.Key + "__LOOP__", "nextAction", loopStatusTable_i.Value.AssignedNextAction);
                            Logger.Info("Set 19");

                        }


                    }

                }

            }


            return visitedForSort;

        }


        /// <summary>
        /// Set values to actionListBranchAdded
        /// </summary>
        /// <param name="actionListBranchAdded"></param>
        /// <param name="target"></param>
        /// <param name="token"></param>
        /// <param name="newValue"></param>
        private static void SetValueToALBA(List<JToken> actionListBranchAdded, string target, string token, string newValue)
        {
            var nextAction = actionListBranchAdded.Where(x => (string)x["id"] == target).Select(x => x).FirstOrDefault().SelectToken(token);

            if (nextAction != null)
            {
                actionListBranchAdded.Where(x => (string)x["id"] == target).Select(x => x).FirstOrDefault().SelectToken(token).Replace(newValue);
                Logger.Info($"Update {token} actionListBranchAdded: {target}=>{newValue}");

            }
            else
            {
                actionListBranchAdded.Where(x => (string)x["id"] == target).FirstOrDefault().SelectToken("id").Parent.AddAfterSelf(new JProperty(token, new JValue(newValue)));
                Logger.Info($"Add {token} actionListBranchAdded: {target}=>{newValue}");
            }

        }

        /// <summary>
        /// Return token as string from actionListBranchAdded
        /// </summary>
        /// <param name="actionListBranchAdded"></param>
        /// <param name="target"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        private static string GetTokenStringFromALBA(List<JToken> actionListBranchAdded, string target, string token)
        {
            return actionListBranchAdded.Where(x => (string)x["id"] == target).Select(x => x.SelectToken(token)).FirstOrDefault()?.ToString();

        }


        /// <summary>
        /// Add jValue to ActionList
        /// </summary>
        /// <param name="id"></param>
        /// <param name="trackingId"></param>
        /// <param name="name"></param>
        /// <param name="type"></param>
        /// <param name="nextAction"></param>
        /// <returns></returns>
        private static JObject SetJvalue(string id, string trackingId, string name, string type, string nextAction, string parentId)
        {
            JObject jvalue = new()
            {
                { "id", new JValue(id) },
                { "trackingId", new JValue("*" + trackingId + "*") },
                { "name", new JValue(name) },
                { "__type", new JValue(type) }
            };
            if (nextAction != null)
            {
                jvalue.Add("nextAction", new JValue(nextAction));
            }
            if (parentId != null)
            {
                jvalue.Add("parentId", new JValue(parentId));
            }
            return jvalue;
        }

        /// <summary>
        /// Remove UnderScore of SubNode
        /// </summary>
        /// <param name="nodeId"></param>
        /// <returns></returns>
        private static string RemoveUnderScoreOfSubNode(string nodeId)
        {
            if (String.IsNullOrEmpty(nodeId)) return nodeId;
            string tmp_nodeId = nodeId;
            if (tmp_nodeId.Contains("_"))
            {
                nodeId = tmp_nodeId.Substring(0, tmp_nodeId.IndexOf("_"));

            }
            return nodeId;
        }

        /// <summary>
        /// Check if all branch nodes were visited
        /// </summary>
        /// <param name="branchStatusTable"></param>
        /// <param name="loopStatusTable"></param>
        /// <param name="branchesInLoop"></param>
        /// <param name="visited"></param>
        /// <param name="currentLoopId"></param>
        /// <param name="currentVertex"></param>
        /// <returns></returns>
        private static bool IsAllBranchesProcessed(Dictionary<string, BranchStatus> branchStatusTable, Dictionary<string, LoopStatus> loopStatusTable, Dictionary<string, List<string>> branchesInLoop, HashSet<string> visited,string currentLoopId,string currentVertex)
        {
            bool isVisited = false;
            if (branchesInLoop.Any(x => x.Key == currentLoopId))
            {
                // All processed if exists?

                foreach (var item in branchesInLoop.Where(x => x.Key == currentLoopId).SelectMany(x => x.Value).ToList())
                {
                    string branchId = RemoveUnderScoreOfSubNode(item.ToString());
                    isVisited = branchStatusTable.Where(x => x.Key == branchId).SelectMany(x => x.Value.ExitPaths).Where(x => x.ExitPathId == item.ToString()).Select(x => x.IsVisited).FirstOrDefault();

                }

                string nextActionOfcurrentLoopId = loopStatusTable.Where(x => x.Value.IsOpen == true).OrderByDescending(x => x.Value.IsOpenSeq).FirstOrDefault().Value.NextAction;
                if (String.IsNullOrEmpty(nextActionOfcurrentLoopId))
                {
                    nextActionOfcurrentLoopId = loopStatusTable.Where(x => x.Value.IsOpen == true).OrderByDescending(x => x.Value.IsOpenSeq).FirstOrDefault().Value.AssignedNextAction;
                }

                if (!String.IsNullOrEmpty(nextActionOfcurrentLoopId))
                {
                    isVisited = visited.Any(x => x.Contains(nextActionOfcurrentLoopId));

                }

                string canCloseNodeOfcurrentLoopId = loopStatusTable.Where(x => x.Value.IsOpen == true).OrderByDescending(x => x.Value.IsOpenSeq).FirstOrDefault().Value.CanCloseNode;
                if (!String.IsNullOrEmpty(canCloseNodeOfcurrentLoopId))
                {
                    isVisited = visited.Any(x => x.Contains(canCloseNodeOfcurrentLoopId));
                }


            }
            if (loopStatusTable.Any(x => x.Key == currentVertex) && currentVertex != visited.LastOrDefault())
            {
                string nextActionOfcurrentLoopId = loopStatusTable.Where(x => x.Value.IsOpen == true).OrderByDescending(x => x.Value.IsOpenSeq).FirstOrDefault().Value.NextAction;
                if (String.IsNullOrEmpty(nextActionOfcurrentLoopId))
                {
                    nextActionOfcurrentLoopId = loopStatusTable.Where(x => x.Value.IsOpen == true).OrderByDescending(x => x.Value.IsOpenSeq).FirstOrDefault().Value.AssignedNextAction;
                }
                isVisited = visited.Any(x => x.Contains(nextActionOfcurrentLoopId));

                string canCloseNodeOfcurrentLoopId = loopStatusTable.Where(x => x.Value.IsOpen == true).OrderByDescending(x => x.Value.IsOpenSeq).FirstOrDefault().Value.CanCloseNode;
                if (!String.IsNullOrEmpty(canCloseNodeOfcurrentLoopId))
                {
                    isVisited = visited.Any(x => x.Contains(canCloseNodeOfcurrentLoopId));

                }

            }

            return isVisited;
        }

        /// <summary>
        /// Show variables on flow
        /// </summary>
        /// <param name="variablesList"></param>
        /// <param name="maxSecondDescriptionLengh"></param>
        /// <returns></returns>
        private static string CreateVariableDescriotions(List<JToken> variablesList,int maxSecondDescriptionLengh,string type)
        {
            int expCount = variablesList.Count();
            string br = "<br>";
            string cologne = ":";
            string equal = "=";
            int eachLinePaddingLength = expCount.ToString().Length + br.Length + cologne.Length+equal.Length;
            int maxDescLengthEachLine = (maxSecondDescriptionLengh / expCount) - eachLinePaddingLength;

            string result=null;

            for (int i = 0; i < expCount; i++)
            {
                int idx = i+1;
                string expressionText = (string)variablesList[i]["expression"]["text"];
                string variableText = (string)variablesList[i]["variable"]["text"];

                expressionText = expressionText.Trim().Replace("\"", "`").Replace("\\", "").Replace(" ", "");
                variableText = variableText.Trim().Replace("\"", "`").Replace("\\", "").Replace(" ", "");
                string formula = null;

                if(type== "GetAttributesAction")
                {
                    formula = expressionText + equal + variableText;

                }
                else
                {
                    formula = variableText + equal + expressionText;
                }

                if (formula.Length > maxDescLengthEachLine)
                {
                    formula.Substring(0, maxDescLengthEachLine);

                }
                formula = idx.ToString() + cologne + formula + br;
                result = result + formula;

            }

            return result;
        }




        internal class BranchStatus
        {
            internal string NextAction { get; set; } = null;
            internal int ExitPathTotalCount { get; set; } = 0;
            internal int ExitPathVisitedCount { get; set; } = 0;
            internal List<ExitPathStatus> ExitPaths { get; set; }= new();
            internal bool AllProcessed { get; set; } = false;
            internal bool IsBranchOpen { get; set; } = false;
            internal int IsOpenSeq { get; set; } = 0;

        }

        internal class ExitPathStatus
        {
            internal string ExitPathId { get; set; } = null;
            internal bool IsVisited { get; set; } = false;
            internal int IsExitPathOpenSeq { get; set; } = 0;

        }

        internal class LoopStatus
        {
            internal string NextAction { get; set; } = null;
            internal bool IsOpen { get; set; } = false;
            internal int IsOpenSeq { get; set; } = 0;
            internal string CanCloseNode { get; set; } = null;
            internal string AssignedNextAction { get; set; } = null;

        }

    }



}



