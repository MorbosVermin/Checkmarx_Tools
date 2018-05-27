using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Com.WaitWha.Checkmarx.Domain
{
    /// <summary>
    /// Stage of the scan within the queue.
    /// </summary>
    [Serializable]
    [DataContract]
    public class Stage
    {
        [DataMember(Name = "id")]
        public int Id { get; set; }

        /// <summary>
        /// Textual version of the Stage.
        /// </summary>
        [DataMember(Name = "value")]
        public string Value { get; set; }

        /// <summary>
        /// Textual version of the Stage.
        /// </summary>
        /// <returns>Value</returns>
        public override string ToString()
        {
            return this.Value;
        }

    }
}
