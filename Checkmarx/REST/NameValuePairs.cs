using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Com.WaitWha.Checkmarx.REST
{
    /// <summary>
    /// Represets parameter names and their values.
    /// </summary>
    [Serializable]
    public class NameValuePairs : NameValueCollection
    {
        /// <summary>
        /// Ensures that the value given will be URL encoded.
        /// </summary>
        /// <param name="name">Name of parameter.</param>
        /// <param name="value">Value of the parameter (will automatically be URL encoded).</param>
        public override void Add(string name, string value)
        {
            base.Add(name, WebUtility.UrlEncode(value));
        }

        /// <summary>
        /// Returns a JSON string representing this object.
        /// </summary>
        /// <returns></returns>
        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }

        /// <summary>
        /// Returns a string which will concatenate all of the names and values (N=V) with an ampersand (&).
        /// </summary>
        /// <returns>Query String</returns>
        public string ToQueryString()
        {
            StringBuilder sb = new StringBuilder();
            foreach(string key in this.Keys)
            {
                sb.Append(String.Format("{0}={1}&", key, this[key]));
            }

            return sb.ToString().Substring(0, sb.Length - 1);
        }

    }
}
