using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallFlowVisualizer
{
    internal class GenesysCloudParticipantData
    {
        internal string OrgName { get; set; } = null;
        internal string FlowType { get; set; }=null;
        internal string FlowID { get; set; } = null;
        internal string ArchitectFlowName { get; set; } = null;
        internal string FlowName { get; set; } = null;    
        internal string Name { get; set; } = null!;
        internal string Id { get; set; } = null!;
        internal string Type { get; set; } = null!;
        internal List<PDVariables> Variables { get; set; } = new List<PDVariables>();

    }

    internal class PDVariables
    {
        internal string ExpressionText { get; set; } = null;
        internal string VariableText { get; set; } = null;
        internal string Statement { get; set; } = null;
        internal List<MetaData> MetaDataList { get; set; } = new List<MetaData>();
    }

    internal class MetaData
    {
        internal string MetaRefId { get; set; } = null;
        internal string MetaName { get; set; } = null;
    }
}
