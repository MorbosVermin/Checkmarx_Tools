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
    public class Status
    {
        [DataMember(Name = "id")]
        public int Id { get; set; }

        [DataMember(Name = "value")]
        public string Value { get; set; }
    }
}
