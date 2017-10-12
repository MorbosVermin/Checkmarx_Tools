using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Com.WaitWha.Checkmarx.SOAP
{
    public class AuthenticationException : Exception
    {
        public AuthenticationException() : base("A session is required to make this SOAP API call. Use Login() prior to making this call to establish a session.") { }
    }
}
