using Com.WaitWha.Checkmarx.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Com.WaitWha.Checkmarx.REST
{
    [Serializable]
    [DataContract]
    public class ReportStatusResponse
    {
        [DataMember(Name = "location")]
        public string Location { get; set; }

        [DataMember(Name = "contentType")]
        public string ContentType { get; set; }

        [DataMember(Name = "status")]
        public Status Status { get; set; }
    }
}
