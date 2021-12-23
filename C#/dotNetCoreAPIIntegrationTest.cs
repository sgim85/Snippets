using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Operations.API.Test
{
    [TestClass]
    public class IntegrationTest
    {
        private WebApplicationFactory<Operations.API.Startup> _factory;

        [TestInitialize]
        public void TestInitialize()
        {
            _factory = new WebApplicationFactory<Operations.API.Startup>();
        }

        [TestMethod]
        public async Task FlightsByETD_AuthorizedUser_Success()
        {
            var client = GetHttpClient();

            var response = await client.GetAsync("v1/FlightKeys?startETDUTC=2010-01-10 08:00&endETDUTC=2010-01-10 12:00");

            response.EnsureSuccessStatusCode();
            Assert.AreEqual("application/json; charset=utf-8", response.Content.Headers.ContentType.ToString(), "Content Header is wrong");
        }

        [TestMethod]
        public async Task FlightsByETD_UnAuthorizedUser_Fail()
        {
            var client = GetHttpClient(false);

            var response = await client.GetAsync("v1/FlightKeys?startETDUTC=2010-01-10 08:00&endETDUTC=2010-01-10 12:00");

            Assert.IsFalse(response.IsSuccessStatusCode);
        }

        private HttpClient GetHttpClient(bool enableAuthorization = true)
        {
            var client = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
                        services =>
                        {
                            services.AddAuthorization(
                                      options => {
                                          if (enableAuthorization)
                                              options.AddPolicy("Bearer", policy => policy.Requirements.Add(new CustomAuthorizationRequirement(true)));
                                          else
                                              options.AddPolicy("Bearer", policy => policy.Requirements.Add(new CustomAuthorizationRequirement(false)));
                                      }
                                );

                            services.AddSingleton<IAuthorizationHandler, CustomAuthorizationHandler>();
                        }
                    ))
                    .CreateClient();

            return client;
        }
    }
}
