using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Amazon;
using Amazon.XRay.Recorder.Core;
using Amazon.XRay.Recorder.Handlers.AwsSdk;
using Anemonis.AspNetCore.RequestDecompression;
using AWSDataServices;
using Services;
using DryIoc;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace iBotSotALambda
{
    public class Startup
    {
        public static bool IsRunningInLambda;
        public static IDiagnosticService CurrentDiagnosticService;
        public static string SteamServiceState;
        public static uint SteamAppId;
        public static string SteamWebApiKey;
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
            AWSXRayRecorder recorder = new AWSXRayRecorderBuilder().Build();
            AWSXRayRecorder.InitializeInstance(recorder: recorder);
        }

        public static IConfiguration Configuration { get; private set; }

        // This method gets called by the runtime. Use this method to add services to the container
        public void ConfigureServices(IServiceCollection services)
        {
            if (!IsRunningInLambda)
            {
                services.AddRequestDecompression(options =>
                {
                    options.Providers.Add<GzipDecompressionProvider>();
                    options.Providers.Add<DeflateDecompressionProvider>();
                    options.Providers.Add<BrotliDecompressionProvider>();
                });
                services.AddResponseCompression();
            }
            services.AddControllers();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            bool customExceptionHandling = false;

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else if(customExceptionHandling)
            {
                app.UseExceptionHandler(errorApp =>
                {
                    errorApp.Run(async context =>
                    {
                        context.Response.StatusCode = 500;
                        context.Response.ContentType = "text/html";

                        await context.Response.WriteAsync("<html lang=\"en\"><body>\r\n");
                        await context.Response.WriteAsync("ERROR!<br><br>\r\n");

                        /*
                        var exceptionHandlerPathFeature =
                            context.Features.Get<IExceptionHandlerPathFeature>();

                        if (exceptionHandlerPathFeature?.Error is FileNotFoundException)
                        {
                            await context.Response.WriteAsync(
                                "File error thrown!<br><br>\r\n");
                        }
                        */

                        await context.Response.WriteAsync(
                            "<a href=\"/\">Home</a><br>\r\n");
                        await context.Response.WriteAsync("</body></html>\r\n");
                        await context.Response.WriteAsync(new string(' ', 512));
                    });
                });
            }
            app.UseXRay("iBotSotA");
            AWSSDKHandler.RegisterXRayForAllServices();
            app.UseHttpsRedirection();
            if (!IsRunningInLambda)
            {
                app.UseRequestDecompression();
                app.UseResponseCompression();
            }

            var currDir = env.ContentRootPath;
            var uiPath = Path.Combine(currDir, "uibuild");
            var fileProvider = new PhysicalFileProvider(uiPath);

            DefaultFilesOptions defaultFilesOptions = new DefaultFilesOptions();
            defaultFilesOptions.DefaultFileNames.Clear();
            defaultFilesOptions.DefaultFileNames.Add("index.html");
            defaultFilesOptions.FileProvider = fileProvider;

            app.UseDefaultFiles(defaultFilesOptions);
            app.UseStaticFiles(new StaticFileOptions()
            {
                FileProvider = fileProvider
            });

            app.UseRouting();
            app.UseAuthorization();

            app.Use(async (context, next) =>
            {
                var routeData = context.GetRouteData();
                var routeValues = routeData.Values;
                if (routeValues.TryGetValue("controller", out var controller) &&
                    routeValues.TryGetValue("action", out var action) && CurrentDiagnosticService != null)
                {
                    await CurrentDiagnosticService.ExecAsync((string) controller, async diagSvc =>
                    {
                        await next.Invoke();
                    }, (string) action);
                } else
                    await next.Invoke();
            });

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapFallback("{*url}", async context =>
                {
                    await context.Response.WriteAsync($"Request: {DateTime.UtcNow} Path: {context.Request.Path}");
                });
            });

        }
    }
}
