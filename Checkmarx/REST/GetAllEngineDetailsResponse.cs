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
    public class GetAllEngineDetailsResponse : SimpleJsonObject
    {

        public string uri { get; set; }
        public int minLoc { get; set; }
        public int maxLoc { get; set; }
        public bool isAlive { get; set; }
        public int maxScans { get; set; }

        /// <summary>
        /// Block/unblock an engine – An engine can be set as "Blocked" during or after creation by setting the "isBlocked" boolean parameter to "True". A blocked engine will not be able to receive additional scans requests. If the engine is currently running a scan, the running scan will continue until completion.
        /// Blocking an engine provides an ability to remove an engine once the scan is complete(either temporarily or permanently) without any new scans being appointed to that engine.
        /// A blocked engine can be unblocked at any given time by setting the "isBlocked" boolean parameter to "False". Once unblocked, the engine will continue receiving scan requests.
        /// </summary>
        public bool isBlocked { get; set; }

        public string cxVersion { get; set; }

        public static List<GetAllEngineDetailsResponse> ParseResponse(HttpResponseMessage response)
        {
            string json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            return JsonConvert.DeserializeObject<List<GetAllEngineDetailsResponse>>(json);
        }

        public static GetAllEngineDetailsResponse ParseForOneResponse(HttpResponseMessage response)
        {
            string json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            return JsonConvert.DeserializeObject<GetAllEngineDetailsResponse>(json);
        }
        
    }
}
