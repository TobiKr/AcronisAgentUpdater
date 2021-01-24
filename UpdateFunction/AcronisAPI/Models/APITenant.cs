using System;
using System.Collections.Concurrent;

namespace azuregeek.AZAcronisUpdater.AcronisAPI.Models
{
    public class APITenant
    {
        public Guid TenantID { get; set; }
        public Guid ParentTenantID { get; set; }
        public String Name { get; set; }
        public String ParentTenantName { get; set; }        
        public String TenantKind { get; set; }
        public ConcurrentBag<APIUser> TenantUsers { get; set; }       
    }
}
