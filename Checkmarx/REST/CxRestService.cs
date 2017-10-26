using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Com.WaitWha.Checkmarx.REST
{
    public class CxRestService
    {
        static readonly ILog Log = LogManager.GetLogger(typeof(CxRestService));
        public const string DEFAULT_USER_AGENT = "CxLIB.Net v1.0";
        public static readonly string CX_REST_API = "cxrestapi";
        public static readonly string AUTH_LOGIN = CX_REST_API + "auth/login";
        public static readonly string CX_COOKIE = "CxCookie";
        public static readonly string CX_CSRF_TOKEN = "CXCSRFToken";

        HttpClient Client { get; set; }
        CookieContainer Cookies { get; set; }

        /// <summary>
        /// Returns whether or not the authentication cookies have expired.
        /// </summary>
        public bool IsSessionGood
        {
            get
            {
                if (Cookies.Count == 0)
                    return false;

                bool foundCxCookie = false;
                bool foundCxCSRFToken = false;
                foreach(Cookie cookie in Cookies.GetCookies(Client.BaseAddress))
                {
                    if (cookie.Expired)
                        return false;

                    foundCxCookie = (!foundCxCookie) ? cookie.Name.Equals(CX_COOKIE) : true;
                    foundCxCSRFToken = (!foundCxCSRFToken) ? cookie.Name.Equals(CX_CSRF_TOKEN) : true;
                }

                return foundCxCSRFToken && foundCxCookie;
            }
        }

        public CxRestService(
            Uri baseUri,
            string userAgent = DEFAULT_USER_AGENT)
        {
            Cookies = new CookieContainer();
            HttpClientHandler handler = new HttpClientHandler()
            {
                CookieContainer = Cookies
            };

            Client = new HttpClient(handler)
            {
                BaseAddress = new Uri(baseUri, CX_REST_API),
                MaxResponseContentBufferSize = 65355 * 4
            };

            Client.DefaultRequestHeaders.Accept.Clear();
            Client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            Client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", userAgent);
        }

        ~CxRestService()
        {
            Client = null;
            Cookies = null;
        }

        #region Core HTTP Functionality

        /// <summary>
        /// Sends a GET request to Checkmarx REST API at the requestUri given. If NameValuePairs is given, then this will
        /// appended to the query string for the call. 
        /// </summary>
        /// <param name="requestUri">Request URI</param>
        /// <param name="nameValuePairs">Name Value Pairs (parameter names and values)</param>
        /// <returns></returns>
        async Task<HttpResponseMessage> Get(string requestUri, NameValuePairs nameValuePairs = null)
        {
            if(nameValuePairs != null)
            {
                requestUri += "?"+ nameValuePairs.ToQueryString();
            }
            
            Uri uri = new Uri(Client.BaseAddress, requestUri);
            Log.Debug(String.Format("Sending HTTP GET request to {0}.", uri));
            HttpResponseMessage response = await Client.GetAsync(uri);
            response.EnsureSuccessStatusCode();

            return response;
        }

        /// <summary>
        /// Sends a POST request to Checkmarx at the requestUri (e.g. auth/login) and containing the JSON given.
        /// </summary>
        /// <param name="requestUri">Request URI</param>
        /// <param name="json">JSON</param>
        /// <returns>Response from the server if the status code is 200</returns>
        async Task<HttpResponseMessage> Post(string requestUri, string json)
        {
            StringContent content = new StringContent(json, Encoding.UTF8, "application/json");
            Uri uri = new Uri(Client.BaseAddress, requestUri);
            Log.Debug(String.Format("Sending HTTP POST request to {0}: {1} bytes (application/json)",
                uri,
                Encoding.UTF8.GetByteCount(json)));

            HttpResponseMessage response = await Client.PostAsync(uri, content);
            response.EnsureSuccessStatusCode();
                        
            return response;
        }

        /// <summary>
        /// Sends a HTTP DELETE request to the server at the requestUri given.
        /// </summary>
        /// <param name="requestUri">Request URI</param>
        /// <returns>Response from the server if the status code is 200</returns>
        async Task<HttpResponseMessage> Delete(string requestUri)
        {
            Uri uri = new Uri(Client.BaseAddress, requestUri);
            Log.Debug(String.Format("Sending HTTP DELETE request to {0}.", uri));
            HttpResponseMessage response = await Client.DeleteAsync(uri);
            response.EnsureSuccessStatusCode();

            return response;
        }

        /// <summary>
        /// Sends a HTTP PUT request to the server at the requestUri given. This will append the given
        /// name value pairs into the body of the request as JSON.
        /// </summary>
        /// <param name="requestUri">Request URI</param>
        /// <param name="nameValuePairs">Name value pairs which will be sent in the body of the request as JSON.</param>
        /// <returns>Response from the server if the status code is 200</returns>
        async Task<HttpResponseMessage> Put(string requestUri, NameValuePairs nameValuePairs)
        {
            StringContent content = new StringContent(nameValuePairs.ToJson(), Encoding.UTF8, "application/json");
            return await Put(requestUri, content);            
        }

        /// <summary>
        /// Sends a HTTP PUT request to the server at the requestUri given. This will append the given
        /// name value pairs into the body of the request as JSON.
        /// </summary>
        /// <param name="requestUri">Request URI</param>
        /// <param name="content">Customized string content to send in the request.</param>
        /// <returns>Response from the server if the status code is 200</returns>
        async Task<HttpResponseMessage> Put(string requestUri, StringContent content)
        {
            Uri uri = new Uri(Client.BaseAddress, requestUri);
            Log.Debug(String.Format("Sending HTTP PUT request to {0}", uri));
            HttpResponseMessage response = await Client.PutAsync(requestUri, content);
            response.EnsureSuccessStatusCode();

            return response;
        }

        #endregion

        /// <summary>
        /// Logs into Checkmarx REST API.
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        /// <seealso cref="https://checkmarx.atlassian.net/wiki/spaces/KC/pages/135561432/Authentication+Login"/>
        public async Task<bool> Login(string username, string password)
        {
            Dictionary<string, string> dict = new Dictionary<string, string>()
            {
                { "userName", username },
                { "password", password }
            };

            string json = JsonConvert.SerializeObject(dict);
            Log.Debug(String.Format("Logging into Checkmarx as {0}", username));
            try
            {
                HttpResponseMessage response = await Post(AUTH_LOGIN, json);
                return IsSessionGood;

            }catch(Exception e)
            {
                Log.Error(String.Format("Failed to login to Checkmarx as {0}: {1}", username, e.Message), e);
            }

            return false;
        }

        /// <summary>
        /// Registers a Scan Engine with Checkmarx
        /// </summary>
        /// <param name="name">Name</param>
        /// <param name="uri">URI</param>
        /// <param name="minLOC">Minimum Lines of Code (LOC)</param>
        /// <param name="maxLOC">Maximum Lines of Code (LOC)</param>
        /// <param name="blocked"></param>
        /// <returns>Id of the newly registered engine or -1 on errors.</returns>
        /// <example>
        /// int engineId = await service.RegisterEngine("new scan engine", "http://localhost/cxwebinterface/...");
        /// List<GetAllEngineDetailsResponse> allScanEngines = await service.GetAllEngineDetails();
        /// GetAllEngineDetailsResponse scanEngineDetails = await service.GetEngineDetails(engineId);
        /// bool removed = await service.UnregisterEngine(engineId);
        /// </example>
        /// <seealso cref="https://checkmarx.atlassian.net/wiki/spaces/KC/pages/135201461/Register+Engine"/>
        public async Task<int> RegisterEngine(string name, string uri, int minLOC = 0, int maxLOC = 999999999, bool blocked = false)
        {
            if (!IsSessionGood)
                throw new Exception("Session is expired. Use Login() to establish a new session before calling this method.");

            Dictionary<string, dynamic> dict = new Dictionary<string, dynamic>()
            {
                { "name", name },
                { "uri", uri },
                { "minLOC", minLOC },
                { "maxLOC", maxLOC },
                { "isBlocked", blocked }
            };

            string json = JsonConvert.SerializeObject(dict);
            Log.Debug(String.Format("Registering engine {0}", name));

            try
            {
                HttpResponseMessage response = await Post("sast/engineServers", json);
                string jsonReturned = await response.Content.ReadAsStringAsync();
                Dictionary<string, dynamic> jsonObject = 
                    JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(jsonReturned);
                int engineId = jsonObject["id"];
                Log.Debug(String.Format("Successfully registered engine as ID {0}", engineId));
                return engineId;

            }catch(Exception e)
            {
                Log.Error(String.Format("Failed to register engine {0}: {1}", name, e.Message), e);
            }

            return -1;
        }

        /// <summary>
        /// Unregisters a scan engine from Checkmarx.
        /// </summary>
        /// <param name="engineId">Id of the engine to unregister.</param>
        /// <returns></returns>
        /// <see cref="GetAllEngineDetails"/> 
        /// <seealso cref="https://checkmarx.atlassian.net/wiki/spaces/KC/pages/135692359/Unregister+Engine"/>
        public async Task<bool> UnregisterEngine(int engineId)
        {
            if(!IsSessionGood)
                throw new Exception("Session is expired. Use Login() to establish a new session before calling this method.");

            string requestUri = String.Format("sast/engineServers/{0}", engineId);
            Log.Debug(String.Format("Unregistering engine {0}", engineId));
            try
            {
                HttpResponseMessage response = await Delete(requestUri);
                return true;

            }catch(Exception e)
            {
                Log.Error(String.Format("Unable to unregister engine {0}: {1}", engineId, e.Message), e);
            }

            return false;
        }

        /// <summary>
        /// Returns a list of scan engines registered with Checkmarx. 
        /// </summary>
        /// <returns></returns>
        /// <seealso cref="https://checkmarx.atlassian.net/wiki/spaces/KC/pages/135561316/Get+All+Engine+Details"/>
        public async Task<List<GetAllEngineDetailsResponse>> GetAllEngineDetails()
        {
            if (!IsSessionGood)
                throw new Exception("Session is expired. Use Login() to establish a new session before calling this method.");

            Log.Debug("Attempting to get information for all scan engines");
            try
            {
                HttpResponseMessage response = await Get("sast/engineServers");
                return GetAllEngineDetailsResponse.ParseResponse(response);

            }catch(Exception e)
            {
                Log.Error(String.Format("Unable to get information regarding scan engines: {0}", e.Message), e);
            }

            return null;
        }

        /// <summary>
        /// Returns information for the scan engine with the given engineId.
        /// </summary>
        /// <param name="engineId">Id of the scan engine to get information for.</param>
        /// <returns></returns>
        /// <see cref="GetAllEngineDetails"/> 
        /// <seealso cref="https://checkmarx.atlassian.net/wiki/spaces/KC/pages/135692380/Get+Engine+Details"/>
        public async Task<GetAllEngineDetailsResponse> GetEngineDetails(int engineId)
        {
            if (!IsSessionGood)
                throw new Exception("Session is expired. Use Login() to establish a new session before calling this method.");

            Log.Debug(String.Format("Getting scan engine information: {0}", engineId));
            try
            {
                HttpResponseMessage response = await Get(String.Format("sast/engineServers/{0}", engineId));
                return GetAllEngineDetailsResponse.ParseForOneResponse(response);

            }
            catch (Exception e)
            {
                Log.Error(String.Format("Unable to get information regarding scan engine {1}: {0}", e.Message, engineId), e);
            }

            return null;
        }

        /// <summary>
        /// Updates a scan engine
        /// </summary>
        /// <param name="engineId">Id of the engine to update</param>
        /// <param name="name">New name of the engine</param>
        /// <param name="uri">New URI of the engine</param>
        /// <param name="minLoc">New minimum lines of code (LOC)</param>
        /// <param name="maxLoc">New maximum lines of code (LOC)</param>
        /// <param name="isBlocked">Block/unblock an engine – An engine can be set as "Blocked" during or after creation by setting the "isBlocked" boolean parameter to "True". 
        /// A blocked engine will not be able to receive additional scans requests. If the engine is currently running a scan, the running scan will continue until completion.
        /// Blocking an engine provides an ability to remove an engine once the scan is complete(either temporarily or permanently) without any new scans being appointed to that engine.
        /// A blocked engine can be unblocked at any given time by setting the "isBlocked" boolean parameter to "False". Once unblocked, the engine will continue receiving scan requests.
        /// </param>
        /// <returns></returns>
        /// <seealso cref="https://checkmarx.atlassian.net/wiki/spaces/KC/pages/135594172/Update+Engine"/>
        public async Task<bool> UpdateEngine(int engineId, 
            string name = "", 
            string uri = "", 
            int minLoc = -1, 
            int maxLoc = -1, 
            bool isBlocked = false)
        {
            if (!IsSessionGood)
                throw new Exception("Session is expired. Use Login() to establish a new session before calling this method.");

            Dictionary<string, dynamic> dict = new Dictionary<string, dynamic>();
            dict.Add("isBlocked", isBlocked);

            if (name.Length > 0)
                dict.Add("name", WebUtility.UrlEncode(name));

            if (uri.Length > 0)
                dict.Add("uri", uri);

            if (minLoc > -1)
                dict.Add("minLoc", minLoc);

            if (maxLoc > -1)
                dict.Add("maxLoc", maxLoc);

            string requestUri = String.Format("sast/engineServers/{0}", engineId);
            string json = JsonConvert.SerializeObject(dict);
            StringContent content = new StringContent(json, Encoding.UTF8, "application/json");
            Log.Debug(String.Format("Attempting to update scan engine: {0}", engineId));

            bool ok = false;
            try
            {
                await Put(requestUri, content);
                ok = true;
            }catch(Exception e)
            {
                Log.Error(String.Format("Failed to update scan engine {0}: {1}", engineId, e.Message), e);
            }

            return ok;
        }

        /// <summary>
        /// Returns a list of scans currently inqueued within Checkmarx.
        /// </summary>
        /// <param name="projectId">Optionally only get scans for a specific project.</param>
        /// <returns></returns>
        /// <seealso cref="https://checkmarx.atlassian.net/wiki/spaces/KC/pages/158302262/Get+All+Scan+in+Queue"/>
        public async Task<List<GetAllScanInQueueResponse>> GetAllScanInQueue(int projectId = -1)
        {
            if (!IsSessionGood)
                throw new Exception("Session is expired. Use Login() to establish a new session before calling this method.");

            string requestUri = "sast/scanQueue";
            NameValuePairs nvPairs = null;
            if (projectId > 0)
            {
                nvPairs = new NameValuePairs();
                nvPairs.Add("projectId", projectId + "");
            }

            Log.Debug(String.Format("Getting scan queue for project: {0}", projectId));
            try
            {
                HttpResponseMessage response = await Get(requestUri, nvPairs);
                return GetAllScanInQueueResponse.ParseResponse(response);

            }catch(Exception e)
            {
                Log.Error(String.Format("Unable to get list of scans: {0}", e.Message), e);
            }

            return null;
        }

    }
}
