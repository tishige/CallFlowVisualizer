using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallFlowVisualizer
{

    public class GcSettings
    {
        public string GcProfileFileName { get; set; }
        public int PageSize { get; set; }
        public int MaxRetryTimeSec { get; set; }
        public int RetryMax { get; set; }

    }

    public class ProxySettings
    {
        public bool UseProxy { get; set; }
        public string ProxyServerAddress { get; set; }

    }

    public class CfvSettings
    {
        public bool AppendDateTimeToFileName { get; set; }
        public bool AppendGcFlowIdToFileName { get; set; }
        public bool AppendGcFlowTypeToFileName { get; set; }
        public bool AppendGcOrgNameToFileName { get; set; }
        public List<string> ConditionNodeList { get; set; }
        public bool CreateParticipantDataList { get; set; }

    }

    public class DrawioSettings
    {
        public bool ColorNode { get; set; }
        public bool NodeRound { get; set; }
        public bool LineRound { get; set; }
        public int Nodespacing { get; set; }
        public int Levelspacing { get; set; }
        public bool ReplaceSpecialCharacter { get; set; }
        public int MaxRetryCount { get; set; }
        public bool ConvertToVisio { get; set; }
        public bool ConvertToPng { get; set; }
        public bool DisableAcceleration { get; set; }


    }

}
