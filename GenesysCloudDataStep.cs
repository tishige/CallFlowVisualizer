using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallFlowVisualizer
{
    internal class GenesysCloudDataStep
    {
        internal string OrgName { get; set; } = null;
        internal string FlowType { get; set; } = null;
        internal string FlowID { get; set; } = null;
        internal string ArchitectFlowName { get; set; } = null;
        internal string FlowName { get; set; } = null;
        internal string Name { get; set; } = null!;
        internal string Id { get; set; } = null!;
        internal string Type { get; set; } = null!;
        internal string DataTableId { get; set; } = null!; // For DataTableLookupAction Only
		internal string DataTableName { get; set; } = null!; // For DataTableLookupAction Only
		internal string DataActionName { get; set; } = null!; // For DataAction Only
		internal List<Inputs> inputs { get; set; } = new List<Inputs>();
		internal List<Outputs> outputs { get; set; } = new List<Outputs>();

	}

	internal class Inputs
	{
		internal string name { get; set; } = null;
		internal string text { get; set; } = null;
	}

	internal class Outputs
	{
		internal string name { get; set; } = null;
		internal string text { get; set; } = null;
	}

}