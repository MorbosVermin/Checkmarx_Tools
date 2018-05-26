using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Com.WaitWha.Checkmarx.Domain
{
    /// <summary>
    /// A preset is a grouping of queries within Checkmarx which are executed during a scan.
    /// </summary>
    [Serializable]
    [DataContract]
    public class Preset : LinkableObject
    {
        [DataMember(Name = "id")]
        public int Id { get; set; }

        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "ownerName")]
        public string OwnerName { get; set; }
    }
}
