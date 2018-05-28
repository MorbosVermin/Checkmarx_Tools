﻿using Com.WaitWha.Checkmarx.Domain;
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
    public class ReportRequest
    {
        [DataMember(Name = "reportType")]
        public ReportTypes ReportType { get; set; }

        [DataMember(Name = "scanId")]
        public long ScanId { get; set; }
    }
}