using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PB.ApiExtensions.OpenApi
{
    public class OpenAPIConfig
    {
        public string Title { get; set; }
        public string Version { get; set; }
        public string Description { get; set; }
        public AWSIntegration AWSIntegration { get; set; }
    }

    public class AWSIntegration
    {
        public bool Enabled { get; set; }
        public APIGateway APIGateway { get; set; }
    }

    public class APIGateway
    {
        public string ApiGatewayUrl { get; set; } // api's proxy url in api gateway
        public string BaseUrl { get; set; } // api base url
        public bool EnableCORS { get; set; }
        public Cognito Cognito { get; set; }
    }

    public class Cognito
    {
        public bool Enabled { get; set; }
        public string UserPool { get; set; }
        public List<string> Scopes { get; set; }
        public string Authorizer { get; set; }
    }
}
