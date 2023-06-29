using CommandLine;

namespace CallFlowVisualizer
{
    /// <summary>
    /// CommandLine arguments
    /// </summary>
    internal class Options
    {
        // Call drawio for CallFlowVisualizer
        [Option('d', "drawio", Required = false, HelpText = "Call drawio.exe for CallFlowVisualizer")]
        public bool drawio { get; set; } = false;

        // Fetch flow from Genesys Cloud
        [Option('f', "fetch",Required = false, HelpText = "Fetch latest Architect flow from GenesysCloud with flow Id")]
        public string flowId { get; set; } = null!;

        // Fetch flow from Genesys Cloud
        [Option('p', "profile", Required = false, Default="default",HelpText = "PureConnect Attendant Profile")]
        public string profile { get; set; } = null!;

        // Call drawio.exe for CallFlowVisualizer and Convert to VISIO format
        [Option('v', "visio", Required = false, HelpText = "Call drawio.exe for CallFlowVisualizer and Convert to VISIO format")]
        public bool visio { get; set; } = false;

        // [ADD] 2023/06/29
        // Call drawio.exe for CallFlowVisualizer and Convert to png format
        [Option('g', "png", Required = false, HelpText = "Call drawio.exe for CallFlowVisualizer and Convert to png format")]
        public bool png { get; set; } = false;

        // Call drawio.exe for CallFlowVisualizer and Convert to VISIO format
        [Option('a', "architect", Required = false, HelpText = "Read all json files in Architect folder")]
        public bool architect { get; set; } = false;

        // Create Participant Data list of Architect flow
        [Option('l', "list", Required = false, HelpText = "Create Participant Data list of Architect flow ")]
        public bool createParticipantDataList { get; set; } = false;

        // [ADD] 2023/06/29
        // Fetch flow from Genesys Cloud
        [Option('n', "name", Required = false, HelpText = "Fetch latest Architect flow from GenesysCloud with name")]
        public string flowName { get; set; } = null!;

        [Option('t', "type",Separator =',', Required = false, HelpText = "Fetch latest Architect flow from GenesysCloud with specified flow type")]
        public IEnumerable<string> flowType { get; set; } = null!;

        // Set file name at arg[0]
        [Value(0,Hidden =true)]
        public string Filename { get; set; } = null!;

        // Show node id on diagram
        [Option("debug", Required = false, HelpText = "Show node id on diagram")]
        public bool debug { get; set; } = false!;

    }

}

