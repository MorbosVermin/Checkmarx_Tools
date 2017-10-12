using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Com.WaitWha.Checkmarx.SOAP
{
    public class CxUtils
    {
        /// <summary>
        /// Converts the given CxDateTime to normal .Net DateTime.
        /// </summary>
        /// <param name="datetime">CxDateTime to convert</param>
        /// <returns>DateTime</returns>
        public static DateTime FromCxDateTime(CxSDKWebService.CxDateTime datetime)
        {
            return new DateTime(datetime.Year, datetime.Month, datetime.Day, datetime.Hour, datetime.Minute, datetime.Second);
        }

    }
}
