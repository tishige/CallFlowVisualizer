using NLog;
using PureCloudPlatform.Client.V2.Api;
using PureCloudPlatform.Client.V2.Client;
using PureCloudPlatform.Client.V2.Model;
using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Configuration;
using ShellProgressBar;
using System.Text;

namespace CallFlowVisualizer
{
    // v1.5.0
    internal class FetchAudioPrompt
    {
        internal static NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        internal static List<GenesysCloudFlowNode> FetchPromptDescription(List<GenesysCloudFlowNode> flowNodesList,Options opt)
        {
            var configRoot = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile(path: "appsettings.json").Build();
            int maxSecondDescriptionLengh = configRoot.GetSection("cfvSettings").Get<CfvSettings>().MaxSecondDescriptionLengh;

            ArchitectApi gcArchApi = new();

            if (gcArchApi.Configuration.AccessToken == null)
            {
                FetchGCAccessToken.GetAccessToken(opt.profile);
            }

            var page = 1;
            int pageSize = configRoot.GetSection("gcSettings").Get<GcSettings>().PageSize;
            int pageCount;

            PromptEntityListing promptEntityListing = new PromptEntityListing();
            string br = "<br>";

            foreach (var HasMetaflowNode in flowNodesList.Where(x => x.AudioMetaData.Count > 0))
            {

                string promptEntity = null;
                string description = null;
                string promptName = null;
                StringBuilder descriptionBuilder = new StringBuilder();
                int seq = 1;
                foreach (var HasMetaflowNode_each in HasMetaflowNode.AudioMetaData)
                {

                    if (HasMetaflowNode_each.Contains("Prompt."))
                    {
                        try
                        {
                            Logger.Info($"Fetch Prompt Description Entities Page:{page}");
                            promptName = HasMetaflowNode_each.Replace("Prompt.", "");

                            List<string> names = new List<string>() { promptName };
                            promptEntityListing = gcArchApi.GetArchitectPrompts(pageNumber: 1, name: names);

                            promptEntity = promptEntityListing.Entities.Select(e => e.Description).FirstOrDefault();

                        }
                        catch (Exception e)
                        {
                            Debug.Print("Exception when calling Architect.prompts: " + e.Message);
                            ColorConsole.WriteError("Exception when calling Architect.prompts: " + e.Message);
                            ColorConsole.WriteError("Updating prompt details has been aborted due to an error.");

                            return flowNodesList;
                        }

                        if (String.IsNullOrEmpty(promptEntity))
                        {
                            promptEntity = seq.ToString()+":Prompt "+ promptName;
                        }
                        else
                        {
                            promptEntity = seq.ToString() + ":Prompt " + promptEntity;
                        }

                    }
                    else
                    {
                        promptEntity = seq.ToString() + ":" + HasMetaflowNode_each;
                    }

                    if (descriptionBuilder.Length > 0)
                    {
                        descriptionBuilder.Append(br);
                    }
                    descriptionBuilder.Append(promptEntity);
                    seq++;
                }


                description = descriptionBuilder.ToString();
                if (description.Length > maxSecondDescriptionLengh)
                {
                    var descSplit = description.Split(new string[] { "<br>" }, StringSplitOptions.None);
                    var newDescription = new StringBuilder();
                    int remainingLength = maxSecondDescriptionLengh;
                    int avgLengthPerLine = remainingLength / descSplit.Length;

                    foreach (var desc in descSplit)
                    {
                        if (desc.Length <= avgLengthPerLine)
                        {
                            newDescription.Append(desc + "<br>");
                            remainingLength -= desc.Length;
                        }
                        else
                        {
                            string truncatedDesc = desc.Substring(0, avgLengthPerLine);
                            newDescription.Append(truncatedDesc + "<br>");
                            remainingLength -= truncatedDesc.Length;
                        }

                        if (remainingLength <= 0)
                        {
                            break;
                        }
                    }

                    description = newDescription.ToString();
                }

                HasMetaflowNode.Desc2 = description;

                var itemToUpdate = flowNodesList.FirstOrDefault(x => x.Id == HasMetaflowNode.Id);

                if (itemToUpdate != null)
                {
                    itemToUpdate.Desc2 = description;
                }
            }

            Logger.Info($"Fetch Prompt Description done");
            
            return flowNodesList;

        }

    }

}

