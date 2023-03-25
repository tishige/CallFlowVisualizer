using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallFlowVisualizer
{
    internal class GenesysCloudFlowNode
    {
        internal string Name { get; set; } = null!;
        internal string Id { get; set; } = null!;
        internal string Type { get; set; } = null!;
        internal string NextAction { get; set; } = null!;
        internal string InQueueFlowName { get; set; } = null!;
        internal string Queues { get; set; } = null!;
        internal string Skills { get; set; } = null!;
        internal string Priority { get; set; } = null!;
        internal string LanguageSkill { get; set; } = null!;
        internal string PreferredAgents { get; set; } = null!;
        internal string Desc2 { get; set; } = null!;
        internal List<string> ParentId { get; set; } = new List<string>();
        internal List<Dictionary<string, string>> Path { get; set; } = new List<Dictionary<string, string>>();
        internal Dictionary<string, Dictionary<string, string>> PathNode { get; set; } = new Dictionary<string, Dictionary<string, string>>();
        internal int Seq { get; set; } = 0;
        internal bool IsAllWithoutPath { get; set; } = false;
        internal string Digit { get; set; } = null!;
        internal string AudioFile { get; set; } = null!;
        internal string FlowGroup { get; set; } = null!;

    }
}
