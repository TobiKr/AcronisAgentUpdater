using System;

namespace azuregeek.AZAcronisUpdater.AcronisAPI.Models
{
    public class APIUser
    {
        public string UserName { get; set; }
        public string Firstname { get; set; }
        public string Lastname { get; set; }
        public string Email { get; set; }
        public Guid UserID { get; set; }
        public Guid TenantID { get; set; }
        public Guid PersonalTenantID { get; set; }
    }
}
