
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
    internal class CreateMarkdown
    {

        //[ADD]2025/05/07 v1.8.2
        internal static void CreateDataStepReferenceMD(List<GenesysCloudDataStep> gcDATAStepList)
        {
            string currentPath = Directory.GetCurrentDirectory();
            createCSVFolder(currentPath);

            string csvfilename = Path.Combine(currentPath, "csv", "DATAStepsReference" + "_" + DateTime.Now.ToString(@"yyyyMMdd-HHmmss") + ".md");
           

            var gcDataStepGroupbyID = gcDATAStepList.GroupBy(x => x.FlowID).ToList();

            // The file need to be UTF-8 without BOM
            using (var streamWriter = new StreamWriter(csvfilename, false, Encoding.Default))
            {
				streamWriter.WriteLine("# Genesys Cloud DATA Steps Reference Documents");

                if (gcDATAStepList.Count == 0)
				{
					streamWriter.WriteLine("No DATA Steps");
					return;
				}


				streamWriter.WriteLine("### Orgnization Name:" + gcDATAStepList[0].OrgName);
				streamWriter.WriteLine("---");

				foreach (var gcDataStepGroupbyID_i in gcDataStepGroupbyID)
				{
					streamWriter.WriteLine("## "+ gcDataStepGroupbyID_i.Select(x=>x.ArchitectFlowName).First());
					streamWriter.WriteLine("- **Flow Name**:" + gcDataStepGroupbyID_i.Select(x => x.FlowName).First());
					streamWriter.WriteLine("- **Flow Id**:" + gcDataStepGroupbyID_i.Select(x => x.FlowID).First());
					streamWriter.WriteLine("- **Flow Type**:" + gcDataStepGroupbyID_i.Select(x => x.FlowType).First());

					var dataActions = gcDataStepGroupbyID_i.Where(x => x.Type == "DataAction").ToList();
					streamWriter.WriteLine("### Data Actions");
					if (dataActions.Count == 0)
					{
						streamWriter.WriteLine("    No Data Actions");
                    }
                    else
                    {
						foreach (var dataAction_i in dataActions)
						{
							streamWriter.WriteLine("1. **" + dataAction_i.Name + "**");
							streamWriter.WriteLine("    - **Data Action Name**:" + dataAction_i.DataActionName);
							streamWriter.WriteLine("    - **Reference Number**:" + dataAction_i.Id);

							streamWriter.WriteLine("    - **Input**");
							streamWriter.WriteLine("        | Key | Value |");
							streamWriter.WriteLine("        | ---- | ---- |");
							foreach (var input in dataAction_i.inputs)
							{
								streamWriter.WriteLine("        | " + input.name + " | " + input.text + " |");
							}

							streamWriter.WriteLine("    - **Output**");

							if (dataAction_i.outputs.Count == 0)
							{
								streamWriter.WriteLine("    No Output");
							}
							else
							{
								streamWriter.WriteLine("        | Key | Value |");
								streamWriter.WriteLine("        | ---- | ---- |");

								foreach (var output in dataAction_i.outputs)
								{
									streamWriter.WriteLine("        | " + output.name + " | " + output.text + " |");
								}


							}


						}

					}

					var dataTableLookupAction = gcDataStepGroupbyID_i.Where(x => x.Type == "DataTableLookupAction").ToList();
					streamWriter.WriteLine("### Data Table Lookups");

					if (dataTableLookupAction.Count == 0)
					{
						streamWriter.WriteLine("    No Data Table Lookup Actions");

					}
					else
					{
						foreach (var dataTableLookupAction_i in dataTableLookupAction)
						{
							streamWriter.WriteLine("1. **" + dataTableLookupAction_i.Name + "**");
							streamWriter.WriteLine("    - **Data Table Name**:" + dataTableLookupAction_i.DataTableName);
							streamWriter.WriteLine("    - **Reference Number**:" + dataTableLookupAction_i.Id);
							streamWriter.WriteLine("    - **Data Table Id**:" + dataTableLookupAction_i.DataTableId);

							streamWriter.WriteLine("    - **Input**");
							streamWriter.WriteLine("        | Key | Value |");
							streamWriter.WriteLine("        | ---- | ---- |");
							foreach (var input in dataTableLookupAction_i.inputs)
							{
								streamWriter.WriteLine("        | " + input.name + " | " + input.text + " |");
							}

							streamWriter.WriteLine("    - **Output**");

							if (dataTableLookupAction_i.outputs.Count == 0)
							{
								streamWriter.WriteLine("    No Output");
							}
							else
							{
								streamWriter.WriteLine("        | Key | Text |");
								streamWriter.WriteLine("        | ---- | ---- |");

								foreach (var output in dataTableLookupAction_i.outputs)
								{
									streamWriter.WriteLine("        | " + output.name + " | " + output.text + " |");
								}

							}



						}

					}
					streamWriter.WriteLine("---");

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

		//[ADD] 2024/10/27
		private static void createCSVFolderWithOrgName(string currentPath)
		{
			try
			{
				if (!Directory.Exists(currentPath))
					Directory.CreateDirectory(currentPath);

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

        // Replace , to . in Flow name
		private static string replaceCommaToPeriod(string str)
		{
			if (str == null)
			{
				return null;

			}

			return str.Replace(",", ".");
		}

		// Remove "" in name field for CSV
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
