using System;
using System.Collections.Generic;
using System.Text;
using RestSharp;
using RestSharp.Authenticators;

namespace azuregeek.AZAcronisUpdater.AcronisAPI
{
    public interface IAcronisAuthenticator : IAuthenticator
    {
        public String AuthToken { get; }
    }
}
