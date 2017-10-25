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

                    foundCxCookie = cookie.Name.Equals(CX_COOKIE);
                    foundCxCSRFToken = cookie.Name.Equals(CX_CSRF_TOKEN);
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
        /// Logs into Checkmarx REST API.
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <returns></returns>
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
        /// <returns></returns>
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

    }
}
