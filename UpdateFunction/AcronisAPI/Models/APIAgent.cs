using System;
using System.Collections.Generic;
using System.Text;

namespace azuregeek.AZAcronisUpdater.AcronisAPI.Models
{
    public class APIAgent
    {
        public Guid AgentID { get; set; }
        public Guid TenantID { get; set; }
        public string Hostname { get; set; }
        public string AgentVersion { get; set; }
        public string OS { get; set; }
        public bool Online { get; set; }
        public bool UpdateAvailable { get; set; }
        public string AvailableUpdateVersion { get; set; }
    }
}
