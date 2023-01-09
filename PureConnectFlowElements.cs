using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallFlowVisualizer
{
    internal class PureConnectFlowElements
    {
            internal string Name { get; set; } = null!;
            internal string Type { get; set; } = null!;
            internal string NodePath { get; set; } = null!;
            internal string ParentNodePath { get; set; } = null!;
            internal string MenuDigits { get; set; } = null!;
            internal string Digit { get; set; } = null!;
            internal string AudioFile { get; set; } = null!;
            internal string Action { get; set; } = null!;
            internal string ScheduleRef { get; set; } = null!;
            internal string Workgroup { get; set; } = null!;
            internal string Skills { get; set; } = null!;
            internal string StationGroup { get; set; } = null!;
            internal string MenuActions { get; set; } = null!;
            internal string Active { get; set; } = null!;
            internal string DNISString { get; set; } = null!;
            internal string Default { get; set; } = null!;
            internal string Subroutine { get; set; } = null!;
            internal List<string> ParentNodePath2 { get; set; } = new List<string>();

    }
}
