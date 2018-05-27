using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Com.WaitWha.Checkmarx.Domain
{
    /// <summary>
    /// An engine which is performing a scan.
    /// </summary>
    [Serializable]
    [DataContract]
    public class Engine : LinkableObject
    {
        [DataMember(Name = "id")]
        public int Id { get; set; }

    }
}
