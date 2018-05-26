using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Com.WaitWha.Checkmarx.Domain
{
    [Serializable]
    [DataContract]
    public class ScanSettings
    {

        [DataMember(Name = "projectId")]
        public int ProjectId { get; set; }

        [DataMember(Name = "presetId")]
        public int PresetId { get; set; }

        [DataMember(Name = "engineConfigurationId")]
        public int EngineConfigurationId { get; set; }

    }
}
