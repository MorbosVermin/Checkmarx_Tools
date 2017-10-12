using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace Com.WaitWha.Checkmarx.SOAP
{
    public class ResponseException : Exception
    {
        EndpointAddress endpoint;

        /// <summary>
        /// Endpoint which created this exception
        /// </summary>
        public EndpointAddress EndpointAddress { get { return endpoint; } }

        public ResponseException(string message, EndpointAddress endpoint) : base(message)
        {
            this.endpoint = endpoint;
        }

        public override string ToString()
        {
            return String.Format("{0} (from {1})", Message, EndpointAddress);
        }

    }
}
