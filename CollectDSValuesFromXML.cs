using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using ShellProgressBar;


namespace CallFlowVisualizer
{
    class CollectDSValuesFromXML
    {
        internal static NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        internal static List<PureConnectFlowElements> CollectDS(string xmlFileName)
        {

            XElement xmllist = XElement.Load(xmlFileName);
            ColorConsole.WriteLine($"Analyzing PureConnect XML file {xmlFileName}", ConsoleColor.Yellow);

            IEnumerable<XElement> allEntries = xmllist.Descendants("ENTRY");
            var profilesEntiriesList = allEntries.Select(x => x.Attribute("CLASS")).Where(y => y.Value == "Profile");
            var schedulesEntiriesList = allEntries.Select(x => x.Attribute("CLASS")).Where(y => y.Value == "Schedule");
            var attendantNodeEntiriesList = allEntries.Select(x => x.Attribute("CLASS")).Where(y => y.Value == "AttendantNode");

            IEnumerable<XElement> allATTRIBUTE = xmllist.Descendants("ATTRIBUTE");
            List<string> nodeNameList = allATTRIBUTE.Select(x => x.Attribute("NAME")).Where(y => y.Value == "Name").Select(y => y.Parent).Select(z => z.Value).ToList();
            List<string> nodeFullPathList = allATTRIBUTE.Select(x => x.Attribute("NAME")).Where(y => y.Value == "FullNodePath").Select(y => y.Parent).Select(z => z.Value).ToList();
            List<PureConnectFlowElements> flowElementsList = new();

            ColorConsole.WriteLine($"{profilesEntiriesList.Count()} profiles found in {xmlFileName}", ConsoleColor.Yellow);

            var pboptions = new ProgressBarOptions
            {
                ProgressCharacter = '─',
                ProgressBarOnBottom = true
            };
            var pcpb = new ProgressBar(nodeFullPathList.Count, "Analyzing XML file...", pboptions);

            foreach (var node_i in nodeFullPathList)
            {

                PureConnectFlowElements flowElements = new PureConnectFlowElements();

                flowElements.NodePath = node_i;
                flowElements.Name = QueryAttrValue(allATTRIBUTE, node_i, "Name");
                flowElements.Type = QueryAttrValue(allATTRIBUTE, node_i, "Type");
                flowElements.Active = QueryAttrValue(allATTRIBUTE, node_i, "Active");
                flowElements.AudioFile = QueryAttrValue(allATTRIBUTE, node_i, "AudioFile");
                flowElements.MenuDigits = QueryAttrValue(allATTRIBUTE, node_i, "MenuDigits");
                flowElements.Digit = QueryAttrValue(allATTRIBUTE, node_i, "Digit");
                flowElements.Default = QueryAttrValue(allATTRIBUTE, node_i, "Default");

                flowElements.Workgroup = QueryAttrValue(allATTRIBUTE, node_i, "Workgroup");
                flowElements.Skills = QueryAttrValue(allATTRIBUTE, node_i, "Skills");
                flowElements.DNISString = QueryAttrValue(allATTRIBUTE, node_i, "DNISString");
                flowElements.ScheduleRef = QueryAttrValue(allATTRIBUTE, node_i, "ScheduleRef");
                flowElements.Default = QueryAttrValue(allATTRIBUTE, node_i, "Default");
                flowElements.Subroutine = QueryAttrValue(allATTRIBUTE, node_i, "Subroutine");
                flowElements.StationGroup = QueryAttrValue(allATTRIBUTE, node_i, "StationGroup");

                pcpb.Tick(flowElements.Name);

                if (flowElements.Type == "Profile")
                {
                    flowElements.ParentNodePath = "";

                }
                else
                {
                    flowElements.ParentNodePath = QueryParentNodePath(allATTRIBUTE, node_i, flowElements.Type);
                    // JumpToLocation is unnecessary for Profile and Schedule.
                    if (flowElements.Type != "Schedule")
                    {
                        var jumpToNodes = QueryJumpToNode(allATTRIBUTE, node_i);

                        if (jumpToNodes != null)
                        {
                            flowElements.ParentNodePath2.AddRange(jumpToNodes);

                        }

                    }

                }

                if (!String.IsNullOrEmpty(flowElements.Name)&&flowElements.Type=="Profile")
                {
                    if (String.IsNullOrEmpty(flowElements.DNISString))
                    {
                        Logger.Info($"Profile: {flowElements.Name} DNIS:");

                    }
                    else
                    {
                        Logger.Info($"Profile: {flowElements.Name} DNIS: {flowElements.DNISString.Trim()}");

                    }

                }

                flowElementsList.Add(flowElements);

            }

            Console.WriteLine();
            Console.WriteLine();

            return flowElementsList;

        }

        private static string QueryAttrValue (IEnumerable<XElement> allATTRIBUTE,string eachNode,string searchValue)
        {

            XElement parentNode = allATTRIBUTE.Select(x => x.Attribute("NAME")).Where(y => y.Value == "FullNodePath").Select(y => y.Parent).Where(z => z.Value == eachNode).Select(a => a.Parent).FirstOrDefault();
            XNode firstSiblingFullNodePath = parentNode.FirstNode;

            // If the value is top of flowElements, the value results null by ElementsAfterSelf.
            string parentNodeNameValue = parentNode.Element("ATTRIBUTE").Value;
            string parentNodeNAME = parentNode.Element("ATTRIBUTE").Attribute("NAME").Value;

            string queryResult = firstSiblingFullNodePath.ElementsAfterSelf().Select(x => x.Attribute("NAME")).Where(y => y.Value == searchValue).Select(y => y.Parent).Select(z => z.Value).FirstOrDefault()?.ToString();

            if (queryResult != null)
            {
                return queryResult;

            }

            if (parentNodeNAME == searchValue)
            {
                return parentNodeNameValue;

            }

            return null;
        }

        private static string QueryParentNodePath(IEnumerable<XElement> allATTRIBUTE, string eachNode,string flowElementsType)
        {

            XElement parentNode = allATTRIBUTE.Select(x => x.Attribute("NAME")).Where(y => y.Value == "FullNodePath").Select(y => y.Parent).Where(z => z.Value == eachNode).Select(a => a.Parent).FirstOrDefault();
            string parentNodePath = "";
            if (flowElementsType== "Queue Audio" || flowElementsType == "Queue Repeat")
            {

                string childIndex = parentNode.Descendants("ATTRIBUTE").Select(x => x.Attribute("NAME")).Where(y => y.Value == "ChildIndex").Select(y => y.Parent).Select(z => z.Value).FirstOrDefault()?.ToString();

                if (childIndex== "00000")
                {
                    var parentNodeParent = parentNode.Parent;
                    parentNodePath = parentNodeParent.Descendants("ATTRIBUTE").Select(x => x.Attribute("NAME")).Where(y => y.Value == "FullNodePath").Select(y => y.Parent).Select(z => z.Value).FirstOrDefault()?.ToString();

                    return parentNodePath;

                }

                var previousNode = parentNode.PreviousNode;
                var previousNodeElement = XElement.Parse(previousNode.ToString());
                parentNodePath = previousNodeElement.Descendants("ATTRIBUTE").Select(x => x.Attribute("NAME")).Where(y => y.Value == "FullNodePath").Select(y => y.Parent).Select(z => z.Value).FirstOrDefault()?.ToString();

                return parentNodePath;
            }

            var upperNode = parentNode.Parent;
            parentNodePath = upperNode.Descendants("ATTRIBUTE").Select(x => x.Attribute("NAME")).Where(y => y.Value == "FullNodePath").Select(y => y.Parent).Select(z => z.Value).FirstOrDefault()?.ToString();

            return parentNodePath;
        }

        private static List<string> QueryJumpToNode(IEnumerable<XElement> allATTRIBUTE, string eachNode)
        {

            XElement parentNode = allATTRIBUTE.Select(x => x.Attribute("NAME")).Where(y => y.Value == "FullNodePath").Select(y => y.Parent).Where(z => z.Value == eachNode).Select(a => a.Parent).FirstOrDefault();
            List<String> jumpNodePath = new List<String>();

            // If there is a node points to itself in JumpToLocation, set the value to parentNode2.
            // If there is PlayAudioWorkgroup Transfer under Menu, the line from Menu to Workgroup Transfer will be drawn. Unable to delete this line because Workgroup Transfer and Select Digit are sibling node. 
            XElement jumpToNode = allATTRIBUTE.Select(x => x.Attribute("NAME")).Where(y => y.Value == "JumpLocation").Select(y => y.Parent).Where(z => z.Value == eachNode).Select(a => a.Parent).FirstOrDefault();

            if (jumpToNode != null)
            {
                jumpNodePath = jumpToNode.Descendants("ATTRIBUTE").Select(x => x.Attribute("NAME")).Where(y => y.Value == "FullNodePath").Select(y => y.Parent).Select(z => z.Value).ToList();

                return jumpNodePath;

            }

            return jumpNodePath;

        }


    }


}
