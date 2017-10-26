using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Com.WaitWha.Checkmarx.REST
{
    [Serializable]
    public class GetAllScanInQueueResponse
    {

        public int id { get; set; }
        public string runId { get; set; }
        public int stage { get; set; }
        public SimpleJsonObject project { get; set; }
        public int engineId { get; set; }
        public int loc { get; set; }
        public IList<SimpleJsonObject> languages { get; set; }
        public DateTime dateCreated { get; set; }
        public DateTime queuedOn { get; set; }
        public DateTime engineStartedOn { get; set; }
        public bool isIncremental { get; set; }
        public bool isPublic { get; set; }
        public string origin { get; set; }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }

        public static List<GetAllScanInQueueResponse> ParseResponse(HttpResponseMessage response)
        {
            string json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            return JsonConvert.DeserializeObject<List<GetAllScanInQueueResponse>>(json);
        }

    }
}
