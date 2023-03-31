
using System.Text;
using System.Collections.Generic;
using CsvHelper;
using System.Globalization;
using ShellProgressBar;
using Microsoft.Extensions.Configuration;
using PureCloudPlatform.Client.V2.Client;
using PureCloudPlatform.Client.V2.Model;
using System.Diagnostics;
using PureCloudPlatform.Client.V2.Extensions.Notifications;

namespace CallFlowVisualizer
{
    internal class CreateCSV
    {
        internal static List<string> CreateCSVPureConnect(List<PureConnectFlowElements> flowElementsList)
        {

            var configRoot = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile(path: "appsettings.json").Build();
            var cfvSettings = configRoot.GetSection("cfvSettings").Get<CfvSettings>();
            bool appendDatetime = cfvSettings.AppendDateTimeToFileName;

            var drawIOSettings = configRoot.GetSection("drawioSettings").Get<DrawioSettings>();
            bool colorNode = drawIOSettings.ColorNode;
            bool nodeRound = drawIOSettings.NodeRound;
            bool lineRound = drawIOSettings.LineRound;
            int nodespacing = drawIOSettings.Nodespacing;
            int levelspacing = drawIOSettings.Levelspacing;

            string nodeStyle = getNodeStyle(colorNode, nodeRound);
            string lineStyle = getLineStyle(lineRound);

            List<string> profileNodePath = flowElementsList.Where(x => x.Type == "Profile").Select(x => x.NodePath).ToList();
            List<string> csvFileResultList = new();

            var pboptions = new ProgressBarOptions
            {
                ProgressCharacter = '─',
                ProgressBarOnBottom = true
            };

            var pccsvpb = new ProgressBar(profileNodePath.Count(), "Creating CSV file for PureConnect", pboptions);

            foreach (var path_i in profileNodePath)
            {

                List<PureConnectFlowElements> eachFlowElement = new();

                eachFlowElement = flowElementsList.Where(x => x.NodePath.Contains(path_i)).ToList();
                string profileName = eachFlowElement.Where(x => x.Type == "Profile").Select(x => x.Name).FirstOrDefault()?.ToString();

                if (profileName == null) profileName = "Profile";

                profileName = profileName.Trim().Replace(" ", "_").Replace("&", "and");
                foreach (char c in Path.GetInvalidFileNameChars())
                {

                    profileName = profileName.Replace(c, '_');
                }

                string currentPath = Directory.GetCurrentDirectory();
                createCSVFolder(currentPath);
                string csvfilename;

                if (appendDatetime)
                {
                    csvfilename = Path.Combine(currentPath, "csv", profileName + "_" + DateTime.Now.ToString(@"yyyyMMdd-HHmmss") + ".csv");

                }
                else
                {
                    csvfilename = Path.Combine(currentPath, "csv", profileName + ".csv");

                }

                if (File.Exists(csvfilename))
                {
                    csvfilename = Path.Combine(currentPath, "csv", profileName + "_" + DateTime.Now.ToString(@"yyyyMMdd-HHmmss_fff") + ".csv");

                }

                pccsvpb.Tick(profileName);

                // The file need to be UTF-8 without BOM
                using (var streamWriter = new StreamWriter(csvfilename, false, Encoding.Default))

                using (var csv = new CsvWriter(streamWriter, CultureInfo.InvariantCulture))
                {
                    csv.WriteField("## Attendant Flow");
                    csv.NextRecord();
                    csv.WriteComment(" label: %type%<br>%step%<br>%desc%");
                    csv.NextRecord();
                    csv.WriteComment(nodeStyle);
                    csv.NextRecord();
                    csv.WriteComment(" namespace: csvimport-");
                    csv.NextRecord();
                    csv.WriteComment(" connect: {\"from\":\"refs\", \"to\":\"id\", \"invert\":true,\"fromlabel\":\"Digit\", \"style\":" + "\"" + lineStyle + "\"" + "}");
                    csv.NextRecord();
                    csv.WriteComment(" connect: {\"from\":\"refs2\", \"to\":\"id\", \"invert\":true,\"style\":" + "\"" + lineStyle + "\"" + "}"); // JumpToLocation用 Labelなし
                    csv.NextRecord();
                    csv.WriteComment(" width: auto");
                    csv.NextRecord();
                    csv.WriteComment(" height: auto");
                    csv.NextRecord();
                    csv.WriteComment(" padding: 15");
                    csv.NextRecord();
                    csv.WriteComment(" ignore: id,shape,fill,stroke,refs");
                    csv.NextRecord();
                    csv.WriteComment(" nodespacing: " + nodespacing);
                    csv.NextRecord();
                    csv.WriteComment(" levelspacing: " + levelspacing);
                    csv.NextRecord();
                    csv.WriteComment(" edgespacing: 40");
                    csv.NextRecord();
                    csv.WriteComment(" layout: auto");
                    csv.NextRecord();

                    csv.WriteField("id");
                    csv.WriteField("type");
                    csv.WriteField("step");
                    csv.WriteField("desc");
                    csv.WriteField("Digit");
                    csv.WriteField("fill");
                    csv.WriteField("stroke");
                    csv.WriteField("shape");
                    csv.WriteField("refs");
                    csv.WriteField("refs2");

                    csv.NextRecord();

                    foreach (var element_i in eachFlowElement)
                    {
                        // id
                        csv.WriteField(replaceBackSlash(element_i.NodePath));
                        csv.WriteField(element_i.Type);
                        csv.WriteField(element_i.Name);
                        csv.WriteField(getDescription(element_i, element_i.Type));
                        csv.WriteField(element_i.Digit.Replace("-", "").Replace("X", ""));

                        string[] shapeStyle = getShapeStyle(element_i.Type, null);
                        csv.WriteField(shapeStyle[0]); // fill
                        csv.WriteField(shapeStyle[1]); //stroke
                        csv.WriteField(shapeStyle[2]); //shape
                        // ref
                        csv.WriteField(replaceBackSlash(element_i.ParentNodePath));
                        // ref2
                        csv.WriteField(toStrJumpToNode(element_i.ParentNodePath2));

                        csv.NextRecord();

                    }

                }

                csvFileResultList.Add(csvfilename);

            }

            return csvFileResultList;

        }

        internal static string CreateCSVGenCloud(List<GenesysCloudFlowNode> FlowNodeList, string flowName, string flowId, bool debug, string flowGroup)
        {

            var configRoot = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile(path: "appsettings.json").Build();
            var cfvSettings = configRoot.GetSection("cfvSettings").Get<CfvSettings>();
            bool appendDatetime = cfvSettings.AppendDateTimeToFileName;
            bool appendGcFlowIdToCSVFileName = cfvSettings.AppendGcFlowIdToFileName;
            List<string> conditionList = cfvSettings.ConditionNodeList;

            var drawIOSettings = configRoot.GetSection("drawioSettings").Get<DrawioSettings>();
            bool colorNode = drawIOSettings.ColorNode;
            bool nodeRound = drawIOSettings.NodeRound;
            bool lineRound = drawIOSettings.LineRound;
            int nodespacing = drawIOSettings.Nodespacing;
            int levelspacing = drawIOSettings.Levelspacing;
            bool replaceSpecialCharacter = drawIOSettings.ReplaceSpecialCharacter;

            string nodeStyle = getNodeStyle(colorNode, nodeRound);
            string lineStyle = getLineStyle(lineRound);

            //[ADD-1]2023/03/25
            int maxSecondDescriptionLengh = configRoot.GetSection("cfvSettings").Get<CfvSettings>().MaxSecondDescriptionLengh;


            string currentPath = Directory.GetCurrentDirectory();
            createCSVFolder(currentPath);

            string csvfilename;

            if (!String.IsNullOrEmpty(flowGroup))
            {
                flowName = flowName + "(" + flowGroup + ")";
            }


            if (appendGcFlowIdToCSVFileName)
            {
                flowName = flowName + "_" + flowId;

            }

            if (appendDatetime)
            {
                csvfilename = Path.Combine(currentPath, "csv", flowName + "_" + DateTime.Now.ToString(@"yyyyMMdd-HHmmss_fff") + ".csv");

            }
            else
            {
                csvfilename = Path.Combine(currentPath, "csv", flowName + ".csv");
                if (File.Exists(csvfilename))
                {
                    csvfilename = Path.Combine(currentPath, "csv", flowName + "_" + flowId + ".csv");

                }

            }

            int maxParentIdRef = FlowNodeList.Select(x => x.ParentId).ToList().OrderByDescending(x => x.Count()).Take(1).FirstOrDefault().Count();

            //[ADD] 2023/03/31
            int flowGroupCount = FlowNodeList.Where(x => x.Type == "Start" || x.Type == "Menu").Distinct().Count();
            string flowGroupName = "Architect Flow";

            if (flowGroupCount == 1)
            {
                flowGroupName = FlowNodeList.Where(x => x.Type == "Start" || x.Type == "Menu").Select(x => x.FlowGroup).FirstOrDefault();
            }

            // The file need to be UTF-8 without BOM
            using (var streamWriter = new StreamWriter(csvfilename, false, Encoding.Default))

            using (var csv = new CsvWriter(streamWriter, CultureInfo.InvariantCulture))
            {
                csv.WriteField("## "+flowGroupName);
                csv.NextRecord();
                csv.WriteComment(" label: %type%<br>%desc1%<br>%desc2%");
                csv.NextRecord();
                csv.WriteComment(nodeStyle);
                csv.NextRecord();
                csv.WriteComment(" namespace: csvimport-");
                csv.NextRecord();

                // [C]2023/01/17 fixed
                if (maxParentIdRef == 0)
                {
                    string connectMsg = " connect: {\"from\":\"refs" + maxParentIdRef.ToString() + "\", \"to\":\"id\", \"invert\":true, \"style\":" + "\"" + lineStyle + "\"" + "}";
                    csv.WriteComment(connectMsg);
                    csv.NextRecord();
                }
                else
                {
                    for (int i = 0; i < maxParentIdRef; i++)
                    {
                        string connectMsg = " connect: {\"from\":\"refs" + i.ToString() + "\", \"to\":\"id\", \"invert\":true, \"style\":" + "\"" + lineStyle + "\"" + "}";
                        csv.WriteComment(connectMsg);
                        csv.NextRecord();

                    }

                }

                csv.WriteComment(" width: auto");
                csv.NextRecord();
                csv.WriteComment(" height: auto");
                csv.NextRecord();
                csv.WriteComment(" padding: 15");
                csv.NextRecord();
                csv.WriteComment(" ignore: id,shape,fill,stroke,refs");
                csv.NextRecord();
                csv.WriteComment(" nodespacing: " + nodespacing);
                csv.NextRecord();
                csv.WriteComment(" levelspacing: " + levelspacing);
                csv.NextRecord();
                csv.WriteComment(" edgespacing: 40");
                csv.NextRecord();
                csv.WriteComment(" layout: verticalflow");
                csv.NextRecord();

                csv.WriteField("id");
                csv.WriteField("type");
                csv.WriteField("desc1");
                csv.WriteField("desc2");
                csv.WriteField("fill");
                csv.WriteField("stroke");
                csv.WriteField("shape");

                for (int i = 0; i < maxParentIdRef; i++)
                {
                    string connectMsg = "refs" + i.ToString();
                    csv.WriteField(connectMsg);

                }

                csv.NextRecord();

                foreach (var node_i in FlowNodeList)
                {
                    // id
                    csv.WriteField(node_i.Id);
                    csv.WriteField(node_i.Type);
                    //desc1
                    csv.WriteField(removeDoubleQuotation(node_i.Name));

                    // Show id in flow For Debug
                    if (debug)
                    {
                        var debug1 = node_i.Id;
                        string debug2 = null;

                        if (node_i.NextAction != null && node_i.NextAction.Length > 0)
                        {
                            debug2 = node_i.NextAction;

                        }
                        else
                        {
                            debug2 = "Unknown";

                        }

                        var debug3 = debug1 + " =><br>" + debug2;
                        csv.WriteField(debug3);

                    }
                    else
                    {
                        // desc2
                        string desc2 = node_i.Desc2;
                        if (replaceSpecialCharacter)
                        {
                            desc2 = replaceSpecialCharacters(node_i.Desc2);

                        }

                        desc2 = trancateDescription(desc2, maxSecondDescriptionLengh);
                        csv.WriteField(desc2); //QueueTo Skill etc...
                    }

                    string[] shapeStyle = getShapeStyle(node_i.Type, conditionList);
                    csv.WriteField(shapeStyle[0]); // fill
                    csv.WriteField(shapeStyle[1]); //stroke
                    csv.WriteField(shapeStyle[2]); //shape
                    //ref
                    int i = 0;
                    foreach (var pId in node_i.ParentId)
                    {

                        csv.WriteField(pId);
                        i++;
                    }
                    for (int j = i; j < maxParentIdRef; j++)
                    {
                        csv.WriteField(null);

                    }

                    csv.NextRecord();

                }

            }

            return csvfilename;

        }

        internal static string CreateCSVGenCloudPerPage(List<GenesysCloudFlowNode> FlowNodeList, string flowName, string flowId, bool debug, string flowGroup)
        {
            //[ADD] 2023/03/31
            var configRoot = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile(path: "appsettings.json").Build();
            var cfvSettings = configRoot.GetSection("cfvSettings").Get<CfvSettings>();
            bool appendDatetime = cfvSettings.AppendDateTimeToFileName;
            bool appendGcFlowIdToCSVFileName = cfvSettings.AppendGcFlowIdToFileName;
            List<string> conditionList = cfvSettings.ConditionNodeList;

            var drawIOSettings = configRoot.GetSection("drawioSettings").Get<DrawioSettings>();
            bool colorNode = drawIOSettings.ColorNode;
            bool nodeRound = drawIOSettings.NodeRound;
            bool lineRound = drawIOSettings.LineRound;
            int nodespacing = drawIOSettings.Nodespacing;
            int levelspacing = drawIOSettings.Levelspacing;
            bool replaceSpecialCharacter = drawIOSettings.ReplaceSpecialCharacter;

            string nodeStyle = getNodeStyle(colorNode, nodeRound);
            string lineStyle = getLineStyle(lineRound);

            //[ADD-1]2023/03/25
            int maxSecondDescriptionLengh = configRoot.GetSection("cfvSettings").Get<CfvSettings>().MaxSecondDescriptionLengh;


            string currentPath = Directory.GetCurrentDirectory();
            createCSVFolder(currentPath);

            string csvfilename;

            if (!String.IsNullOrEmpty(flowGroup))
            {
                flowName = flowName + "(" + flowGroup + ")";
            }


            if (appendGcFlowIdToCSVFileName)
            {
                flowName = flowName + "_" + flowId;

            }

            if (appendDatetime)
            {
                csvfilename = Path.Combine(currentPath, "csv", flowName + "_" + DateTime.Now.ToString(@"yyyyMMdd-HHmmss_fff") + ".csv");

            }
            else
            {
                csvfilename = Path.Combine(currentPath, "csv", flowName + ".csv");
                if (File.Exists(csvfilename))
                {
                    csvfilename = Path.Combine(currentPath, "csv", flowName + "_" + flowId + ".csv");

                }

            }

            int maxParentIdRef = FlowNodeList.Select(x => x.ParentId).ToList().OrderByDescending(x => x.Count()).Take(1).FirstOrDefault().Count();

            // The file need to be UTF-8 without BOM
            using (var streamWriter = new StreamWriter(csvfilename, false, Encoding.Default))

            using (var csv = new CsvWriter(streamWriter, CultureInfo.InvariantCulture))
            {
                int flowGroupCount = FlowNodeList.Where(x => x.Type == "Start" || x.Type == "Menu").Distinct().Count();
                bool IsAlreadyWriteFlowGroupHeader = false;
                List<GenesysCloudFlowNode> FlowNodeListGroupSorted = FlowNodeList.OrderBy(x => x.FlowGroup).ThenBy(x => x.Seq).ToList();


                foreach (var node_i in FlowNodeListGroupSorted)
                {
                    if (node_i.Type == "Start" || node_i.Type == "Menu")
                    {

                        string flowGroupName = node_i.FlowGroup;

                        foreach (char c in Path.GetInvalidFileNameChars())
                        {
                            flowGroupName = flowGroupName.Replace(c, '_');
                        }

                        flowGroupName = flowGroupName.Replace(' ', '_');

                        if (!IsAlreadyWriteFlowGroupHeader)
                        {

                            csv.WriteField("## " + flowGroupName);
                            csv.NextRecord();
                            if (flowGroupCount == 1)
                            {
                                IsAlreadyWriteFlowGroupHeader = true;

                            }
                        }

                        csv.WriteComment(" label: %type%<br>%desc1%<br>%desc2%");
                        csv.NextRecord();
                        csv.WriteComment(nodeStyle);
                        csv.NextRecord();
                        csv.WriteComment(" namespace: csvimport-");
                        csv.NextRecord();

                        // [C]2023/01/17 fixed
                        if (maxParentIdRef == 0)
                        {
                            string connectMsg = " connect: {\"from\":\"refs" + maxParentIdRef.ToString() + "\", \"to\":\"id\", \"invert\":true, \"style\":" + "\"" + lineStyle + "\"" + "}";
                            csv.WriteComment(connectMsg);
                            csv.NextRecord();
                        }
                        else
                        {
                            for (int refi = 0; refi < maxParentIdRef; refi++)
                            {
                                string connectMsg = " connect: {\"from\":\"refs" + refi.ToString() + "\", \"to\":\"id\", \"invert\":true, \"style\":" + "\"" + lineStyle + "\"" + "}";
                                csv.WriteComment(connectMsg);
                                csv.NextRecord();

                            }

                        }

                        csv.WriteComment(" width: auto");
                        csv.NextRecord();
                        csv.WriteComment(" height: auto");
                        csv.NextRecord();
                        csv.WriteComment(" padding: 15");
                        csv.NextRecord();
                        csv.WriteComment(" ignore: id,shape,fill,stroke,refs");
                        csv.NextRecord();
                        csv.WriteComment(" nodespacing: " + nodespacing);
                        csv.NextRecord();
                        csv.WriteComment(" levelspacing: " + levelspacing);
                        csv.NextRecord();
                        csv.WriteComment(" edgespacing: 40");
                        csv.NextRecord();
                        csv.WriteComment(" layout: verticalflow");
                        csv.NextRecord();

                        csv.WriteField("id");
                        csv.WriteField("type");
                        csv.WriteField("desc1");
                        csv.WriteField("desc2");
                        csv.WriteField("fill");
                        csv.WriteField("stroke");
                        csv.WriteField("shape");

                        for (int mi = 0; mi < maxParentIdRef; mi++)
                        {
                            string connectMsg = "refs" + mi.ToString();
                            csv.WriteField(connectMsg);

                        }

                        csv.NextRecord();

                    }

                    // id
                    csv.WriteField(node_i.Id);
                    csv.WriteField(node_i.Type);
                    //desc1
                    csv.WriteField(removeDoubleQuotation(node_i.Name));

                    // Show id in flow For Debug
                    if (debug)
                    {
                        var debug1 = node_i.Id;
                        string debug2 = null;

                        if (node_i.NextAction != null && node_i.NextAction.Length > 0)
                        {
                            debug2 = node_i.NextAction;

                        }
                        else
                        {
                            debug2 = "Unknown";

                        }

                        var debug3 = debug1 + " =><br>" + debug2;
                        csv.WriteField(debug3);

                    }
                    else
                    {
                        // desc2
                        string desc2 = node_i.Desc2;
                        if (replaceSpecialCharacter)
                        {
                            desc2 = replaceSpecialCharacters(node_i.Desc2);

                        }

                        desc2 = trancateDescription(desc2, maxSecondDescriptionLengh);
                        csv.WriteField(desc2); //QueueTo Skill etc...
                    }

                    string[] shapeStyle = getShapeStyle(node_i.Type, conditionList);
                    csv.WriteField(shapeStyle[0]); // fill
                    csv.WriteField(shapeStyle[1]); //stroke
                    csv.WriteField(shapeStyle[2]); //shape
                    //ref
                    int i = 0;
                    foreach (var pId in node_i.ParentId)
                    {

                        csv.WriteField(pId);
                        i++;
                    }
                    for (int j = i; j < maxParentIdRef; j++)
                    {
                        csv.WriteField(null);

                    }

                    csv.NextRecord();

                }

            }

            return csvfilename;

        }


        internal static void CreatePDListCSVGenCloud(List<GenesysCloudParticipantData> gcPDList)
        {
            string currentPath = Directory.GetCurrentDirectory();
            createCSVFolder(currentPath);

            string csvfilename = Path.Combine(currentPath, "csv", "PDLIST" + "_" + DateTime.Now.ToString(@"yyyyMMdd-HHmmss") + ".csv");

            // The file need to be UTF-8 without BOM
            using (var streamWriter = new StreamWriter(csvfilename, false, Encoding.Default))

            using (var csv = new CsvWriter(streamWriter, CultureInfo.InvariantCulture))
            {

                csv.WriteField("OrgName");
                csv.WriteField("FlowType");
                csv.WriteField("FlowID");
                csv.WriteField("ArchitectFlowName");
                csv.WriteField("FlowName");
                csv.WriteField("Name");
                csv.WriteField("Id");
                csv.WriteField("Type");
                csv.WriteField("ExpressionText");
                csv.WriteField("VariableText");
                csv.WriteField("Statement");
                csv.WriteField("MetaRefId");
                csv.WriteField("MetaName");
                csv.NextRecord();

                foreach (var gcPDList_i in gcPDList)
                {

                    foreach (var variables_i in gcPDList_i.Variables)
                    {

                        if (variables_i.MetaDataList.Count > 0)
                        {
                            foreach (var metaData_i in variables_i.MetaDataList)
                            {
                                csv.WriteField(gcPDList_i.OrgName);
                                csv.WriteField(gcPDList_i.FlowType);
                                csv.WriteField(gcPDList_i.FlowID);
                                csv.WriteField(gcPDList_i.ArchitectFlowName);
                                csv.WriteField(gcPDList_i.FlowName);
                                csv.WriteField(gcPDList_i.Name);
                                csv.WriteField(gcPDList_i.Id);
                                csv.WriteField(gcPDList_i.Type);

                                csv.WriteField(variables_i.ExpressionText);
                                csv.WriteField(variables_i.VariableText);
                                csv.WriteField(variables_i.Statement);
                                csv.WriteField(metaData_i.MetaRefId);
                                csv.WriteField(metaData_i.MetaName);
                                csv.NextRecord();
                            }


                        }
                        else
                        {
                            csv.WriteField(gcPDList_i.OrgName);
                            csv.WriteField(gcPDList_i.FlowType);
                            csv.WriteField(gcPDList_i.FlowID);
                            csv.WriteField(gcPDList_i.ArchitectFlowName);
                            csv.WriteField(gcPDList_i.FlowName);
                            csv.WriteField(gcPDList_i.Name);
                            csv.WriteField(gcPDList_i.Id);
                            csv.WriteField(gcPDList_i.Type);

                            csv.WriteField(variables_i.ExpressionText);
                            csv.WriteField(variables_i.VariableText);
                            csv.WriteField(variables_i.Statement);
                            csv.WriteField(null);
                            csv.WriteField(null);
                            csv.NextRecord();
                        }

                    }

                }

            }

        }


        // Create CSV folder if it does not exists
        private static void createCSVFolder(string currentPath)
        {
            try
            {
                if (!Directory.Exists(Path.Combine(currentPath, "CSV")))
                    Directory.CreateDirectory(Path.Combine(currentPath, "CSV"));

            }
            catch (Exception)
            {
                ColorConsole.WriteError("Failed to create CSV folder.Check file access permission.");
                Environment.Exit(1);
            }

        }

        // draw.io cannot accept backslash,so replace to _
        private static string replaceBackSlash(string nodePath)
        {
            if (nodePath == null)
            {
                return null;

            }

            return nodePath.Replace("\\", "_");
        }

        // draw.io desktop cannot accept double quote in csv when paste it
        private static string replaceSpecialCharacters(string str)
        {
            if (str == null)
            {
                return null;

            }

            str = str.Replace("\"", "`");
            str = str.Replace("\\", "");

            return str;
        }

        // Remove "" in name field for CSV
        private static string removeDoubleQuotation(string str)
        {
            if (str == null)
            {
                return null;

            }

            return str.Replace("\"", "");
        }

        private static string trancateDescription(string str, int maxSecondDescriptionLengh)
        {
            if (str == null)
            {
                return null;

            }

            str = str.Replace("\n", "").Replace("\r", "");

            if (str.Length >= maxSecondDescriptionLengh)
            {
                return str.Substring(0, maxSecondDescriptionLengh);

            }

            return str;
        }

        private static string toStrJumpToNode(List<string> jumpToNodes)
        {
            string jumpToNode = "";
            if (jumpToNodes == null) return jumpToNode;
            jumpToNode = String.Join(",", jumpToNodes);
            jumpToNode = replaceBackSlash(jumpToNode);
            return jumpToNode;
        }

        // Whether node shape is colored and rounded
        private static string getNodeStyle(bool colorNode, bool nodeRound)
        {
            string style = " style: html=1;shape=%shape%;";
            if (colorNode)
            {
                style = style + "fillColor=%fill%;strokeColor=%stroke%;";

            }
            else
            {
                style = style + "fillColor=#ffffff;strokeColor=#000000;";

            }

            if (nodeRound)
            {
                style = style + "rounded=1;";

            }

            return style;

        }

        // Whether line is rounded
        private static string getLineStyle(bool lineRound)
        {
            string lineStyle = null;

            if (lineRound)
            {
                lineStyle = lineStyle + "edgeStyle=orthogonalEdgeStyle;orthogonalLoop=1;jettySize=auto;curved = 0; endArrow = blockThin; endFill = 1;";

            }
            else
            {
                lineStyle = lineStyle + "edgeStyle=orthogonalEdgeStyle;rounded=0;orthogonalLoop=1;jettySize=auto;curved = 0; endArrow = blockThin; endFill = 1;";

            }

            return lineStyle;

        }

        // Set shape and color according to Type value
        private static string[] getShapeStyle(string nodeType, List<string> conditionList)
        {
            string[] shapeArray = new string[3];
            // PureConnect
            if (nodeType.Contains("Transfer") && nodeType != "TransferPureMatchAction" && !nodeType.Contains("_Sub"))
            {
                shapeArray[0] = "#d5e8d4"; //green
                shapeArray[1] = "#82b366";
                shapeArray[2] = "ellipse";

                return shapeArray;
            }
            if (nodeType == "Schedule")
            {
                shapeArray[0] = "#fff2cc"; //orange
                shapeArray[1] = "#d6b656";
                shapeArray[2] = "rectangle";

                return shapeArray;

            }

            if (nodeType == "Subroutine Initiator")
            {
                shapeArray[0] = "#f8cecc"; //red
                shapeArray[1] = "#b85450";
                shapeArray[2] = "ellipse";

                return shapeArray;

            }

            // GenesysCloud

            if (nodeType == "LoopAction")
            {
                shapeArray[0] = "#e1d5e7"; //purple
                shapeArray[1] = "#9673a6";
                shapeArray[2] = "rhombus";

                return shapeArray;

            }

            if (nodeType == "ExitLoopAction" || nodeType == "LoopAction_Sub")
            {
                shapeArray[0] = "#e1d5e7"; //purple
                shapeArray[1] = "#9673a6";
                shapeArray[2] = "rectangle";

                return shapeArray;

            }


            if (nodeType.Contains("_Sub"))
            {
                shapeArray[0] = "#d5e8d4"; //green
                shapeArray[1] = "#82b366";
                shapeArray[2] = "rectangle";

                return shapeArray;

            }

            if (conditionList != null && conditionList.Where(x => x == nodeType).Any())
            {
                shapeArray[0] = "#fff2cc"; //orange
                shapeArray[1] = "#d6b656";
                shapeArray[2] = "rhombus";

                return shapeArray;
            }

            if (nodeType == "Start" || nodeType == "End")
            {
                shapeArray[0] = "#f8cecc"; //red
                shapeArray[1] = "#b85450";
                shapeArray[2] = "ellipse";

                return shapeArray;
            }

            if (nodeType == "DisconnectAction")
            {
                shapeArray[0] = "#f8cecc"; //orange2
                shapeArray[1] = "#b85450";
                shapeArray[2] = "ellipse";

                return shapeArray;
            }

            shapeArray[0] = "#dae8fc"; //blue
            shapeArray[1] = "#6c8ebf";
            shapeArray[2] = "rectangle";

            return shapeArray;
        }

        // Change bottom step description according to Type value
        private static string getDescription(PureConnectFlowElements flowElement, string nodeType)
        {
            string description = "";
            // PureConnect
            if (nodeType == "Profile")
            {
                if (flowElement.DNISString != null) { description = flowElement.DNISString.Trim(); }

            }

            if (nodeType == "Schedule")
            {
                if (flowElement.ScheduleRef != null) { description = flowElement.ScheduleRef.Trim(); }

                if (description.Length > 25)
                {
                    int pos = 25;
                    do
                    {
                        description = description.Insert(pos, "<br>");
                        pos = pos + 25;

                    } while (description.Length > pos);

                }

            }

            if (nodeType == "Workgroup Transfer")
            {
                description = flowElement.Workgroup + "(" + flowElement.Skills + ")";

            }

            if (nodeType == "StationGroup")
            {
                description = flowElement.StationGroup;

            }

            if (nodeType == "Audio Playback" || nodeType == "Queue Audio")
            {
                description = flowElement.AudioFile;

            }

            if (nodeType == "Menu")
            {
                description = flowElement.MenuDigits;

            }

            return description;

        }

    }

}
