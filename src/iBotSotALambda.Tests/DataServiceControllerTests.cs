using System;
using System.Threading.Tasks;
using Amazon;
using Amazon.DynamoDBv2.Model.Internal.MarshallTransformations;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestUtilities;
using DataServiceCore;
using DryIoc;
using iBotSotALambda.Controllers;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Xunit;

namespace iBotSotALambda.Tests
{
    public class DataServiceControllerTests : IAsyncLifetime
    {
        private uint SteamAppId;
        private string SteamWebApiKey;

        [Fact]
        public async Task SelfAuthenticationTest()
        {
            try
            {
                var container = new Container();
                container.Register<ISteamService, SteamService.SteamService>(Reuse.Singleton);
                var steamService = container.Resolve<ISteamService>();
                steamService.InitService(SteamAppId, SteamWebApiKey);
                steamService.InitSteamClient();

                var authData = await steamService.GetAuthTokenA();
                var authenticated = await steamService.ValidateAuthToken(authData.steamIdValue, authData.authToken);
                var dataServiceController = new DataServiceController();
                Assert.True(authenticated);
            }
            catch (Exception ex)
            {

            }
        }

        [Fact]
        public async Task SelfAuthenticationTestWeb()
        {
            var container = new Container();
            container.Register<ISteamService, SteamService.SteamService>(Reuse.Singleton);
            var steamService = container.Resolve<ISteamService>();
            steamService.InitService(SteamAppId, SteamWebApiKey);
            steamService.InitSteamClient();

            var authData = await steamService.GetAuthTokenA();
            var authDataHex = authData.authToken.ToHexString();
            var authenticated = await steamService.ValidateAuthTokenWeb(authDataHex);
            Assert.True(authenticated);
        }


        [Fact]
        public async Task ServerAuthenticateTest()
        {
            var container = new Container();
            container.Register<ISteamService, SteamService.SteamService>(Reuse.Singleton);

            var steamService = container.Resolve<ISteamService>();
            steamService.InitService(SteamAppId, SteamWebApiKey);

            steamService.InitSteamClient();
            var authData = await steamService.GetAuthTokenA();

            var controller = new DataServiceController();
            var authDataHex = authData.authToken.ToHexString();
            //var result = (JsonResult) await controller.AuthTest(authData.steamIdValue, authDataHex);
            var result = (JsonResult) await controller.AuthTest(authDataHex);
            var expected = new JsonResult(new
            {
                statusMessage = "OK",
                authenticated = true
            });
            Assert.Equal(expected.Value, result.Value);
        }

        public async Task InitializeAsync()
        {
            var parameterClient = new AwsParameterStoreClient(RegionEndpoint.EUWest1);
            var steamAppId = await parameterClient.GetValueAsync("ibotsota-steamappid");
            var steamWebApiKey = await parameterClient.GetValueAsync("	ibotsota-steamwebapikey");
            SteamAppId = uint.Parse(steamAppId);
            SteamWebApiKey = steamWebApiKey;
        }

        public async Task DisposeAsync()
        {
        }
    }

}