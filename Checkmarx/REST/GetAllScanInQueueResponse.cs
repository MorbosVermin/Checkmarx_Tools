using Com.WaitWha.Checkmarx.Domain;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Com.WaitWha.Checkmarx.REST
{
    /// <summary>
    /// GET /sast/sastQueue
    /// </summary>
    [Serializable]
    [DataContract]
    public class GetAllScanInQueueResponse
    {
        [DataMember(Name = "id")]
        public long ScanId { get; set; }

        [DataMember(Name = "stage")]
        public Stage Stage { get; set; }

        [DataMember(Name = "teamId")]
        public string TeamId { get; set; }

        [DataMember(Name = "project")]
        public Project Project { get; set; }

        [DataMember(Name = "engine")]
        public Engine Engine { get; set; }

        [DataMember(Name = "loc")]
        public long LOC { get; set; }

        [DataMember(Name = "languages")]
        public List<Language> Languages { get; set; }

        [DataMember(Name = "dateCreated")]
        public string DateCreated { get; set; }

        [DataMember(Name = "queuedOn")]
        public string QueuedOn { get; set; }

        [DataMember(Name = "engineStartedOn")]
        public string EngineStartedOn { get; set; }

        [DataMember(Name = "isIncremental")]
        public bool IsIncremental { get; set; }

        [DataMember(Name = "isPublic")]
        public bool IsPublic { get; set; }

        [DataMember(Name = "origin")]
        public string Origin { get; set; }

    }
}
