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
    public class Scan
    {
        [DataMember(Name = "projectId")]
        public int ProjectId { get; set; }

        [DataMember(Name = "isIncremenal")]
        public bool isIncremental { get; set; }

        [DataMember(Name = "isPublic")]
        public bool isPublic { get; set; }

        [DataMember(Name = "forceScan")]
        public bool isForceScan { get; set; }

    }
}
