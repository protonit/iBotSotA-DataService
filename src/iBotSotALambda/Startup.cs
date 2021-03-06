using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Amazon;
using Amazon.XRay.Recorder.Core;
using AWSDataService;
using DataServiceCore;
using DryIoc;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace iBotSotALambda
{
    public class Startup
    {
        public static string SteamServiceState;
        public static uint SteamAppId;
        public static string SteamWebApiKey;
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;

            try
            {
                var parameterClient = new AwsParameterStoreClient(RegionEndpoint.EUWest1);
                var asyncTask = Task.Run(async () =>
                {
                    var steamAppId = await parameterClient.GetValueAsync("ibotsota-steamappid");
                    var steamWebApiKey = await parameterClient.GetValueAsync("	ibotsota-steamwebapikey");
                    SteamAppId = uint.Parse(steamAppId);
                    SteamWebApiKey = steamWebApiKey;
                });

                asyncTask.Wait();

                using var container = new Container();

                //container.Register<DynamoDBDataService>(Reuse.Singleton);
                //container.Register<TimestreamDataService>(Reuse.Singleton);
                container.Register<ISteamService, SteamService.SteamService>(Reuse.Singleton);

                var steamService = container.Resolve<ISteamService>();
                steamService.InitService(SteamAppId, SteamWebApiKey);
                SteamServiceState = "OK";

            }
            catch (Exception ex)
            {
                SteamServiceState = ex.ToString();
            }

            AWSXRayRecorder recorder = new AWSXRayRecorderBuilder().Build();
            AWSXRayRecorder.InitializeInstance(recorder: recorder);
        }

        public static IConfiguration Configuration { get; private set; }

        // This method gets called by the runtime. Use this method to add services to the container
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseXRay("iBotSotA");

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapGet("/", async context =>
                {
                    await context.Response.WriteAsync($"Welcome to running ASP.NET Core on AWS Lambda {DateTime.Now} - {SteamServiceState}");
                });
            });

        }
    }
}