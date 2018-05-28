using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Com.WaitWha.Checkmarx.Domain
{
    /// <summary>
    /// Report formats available from Checkmarx.
    /// </summary>
    [Serializable]
    [DataContract]
    public enum ReportTypes
    {
        /// <summary>
        /// Portable Document Format from Adobe.
        /// </summary>
        PDF,

        /// <summary>
        /// Rich-Text Format
        /// </summary>
        RTF,

        /// <summary>
        /// Comma-separated values
        /// </summary>
        CSV,

        /// <summary>
        /// Extensible Markup Language
        /// </summary>
        XML

    }
}
