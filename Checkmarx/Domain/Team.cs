using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Com.WaitWha.Checkmarx.Domain
{
    /// <summary>
    /// A team within Checkmarx.
    /// </summary>
    [Serializable]
    public class Team
    {
        [DataMember(Name = "id")]
        public string Id { get; set; }

        [DataMember(Name = "fullName")]
        public string Name { get; set; }

    }
}
