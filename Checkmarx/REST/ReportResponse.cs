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
    public class ReportResponse
    {
        [DataMember(Name = "reportId")]
        public int ReportId { get; set; }

        [DataMember(Name = "status")]
        public Link Status { get; set; }

        [DataMember(Name = "report")]
        public Link Report { get; set; }
    }
}
