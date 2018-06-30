using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.ModelBinding.Internal;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;
using NLog;

namespace Statia
{
    public class Startup
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private static readonly RequestProcessor _requestProcessor = new RequestProcessor();
        
        public void ConfigureServices(IServiceCollection services)
        {
            var applicationPartManager = new ApplicationPartManager();
            applicationPartManager.ApplicationParts.Add(new AssemblyPart(typeof(Startup).Assembly));
            services.Add(new ServiceDescriptor(typeof(ApplicationPartManager), applicationPartManager));
            services.AddSpaStaticFiles(opt => { opt.RootPath = Program.RootDirectory; });
        }

        private readonly string _responseServerHeader = $"Statia {Program.Version}";

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            Preload();
            app.Use(async (context, next) =>
            {
                
                var stopwatch = new Stopwatch();
                stopwatch.Start();
                var requestUrl = context.Request.Path.Value.ToLower();
                if (requestUrl.Length > 1 && requestUrl.EndsWith('/'))
                {
                    requestUrl = requestUrl.Substring(0, requestUrl.Length - 1);
                }
                context.Response.Headers[HeaderNames.Server] = _responseServerHeader;
                context.Items["pages"] = _urlPageCache;
                context.Items["stopwatch"] = stopwatch;
                context.Items["requestUrl"] = requestUrl;
                var code = await _requestProcessor.ProcessRequest(context);
                if (code.HasValue)
                {
                    if (404.Equals(code.Value))
                    {
                        await _requestProcessor.WriteNotFoundResponse(context);
                        return;
                    }
                    context.Items["code"] = code.Value;
                }
                else
                {
                    await next.Invoke();
                }
                stopwatch.Stop();
                if (code == null)
                {
                    code = context.Response.StatusCode;
                }
                string logMsg = $"{context.Connection.RemoteIpAddress}:{context.Connection.RemotePort}\t{requestUrl}\t{stopwatch.Elapsed.TotalMilliseconds:0.000} ms\t{code}";
                _logger.Info(logMsg);
            });
            app.UseStaticFiles(new StaticFileOptions()
            {
                FileProvider = new PhysicalFileProvider(Program.RootDirectory),
                ServeUnknownFileTypes = true,
                OnPrepareResponse = ctx =>
                {
                    ctx.Context.Response.Headers.Append("Cache-Control", "public,max-age=600");
                },
            });
            app.Run(async (context) =>
            {
                var item = context.Items["code"];
                var code = (int?) item ?? 404;
                context.Response.StatusCode = code;
                if (code == 404)
                {
                    await _requestProcessor.WriteNotFoundResponse(context);
                }
                //string requestUrl = (string) context.Items["requestUrl"];
                //var stopwatch = (Stopwatch) context.Items["stopwatch"];
                
            });
        }
        
        public const string VERSION = "0.1";

        /// <summary>
        /// [en:[relativeUrl:relativeUrl.html,...],...]
        /// </summary>
        private Dictionary<(string, string), string> _urlPageCache;

        private Loader _loader = new Loader(Program.RootDirectory);
        
        private void Preload()
        {
            var sw = new Stopwatch();
            sw.Start();
            Console.WriteLine($"Loading root directory: {Program.RootDirectory}");
            _urlPageCache = _loader.Load();
            sw.Stop();
            Console.WriteLine($"Loaded in {sw.ElapsedMilliseconds} ms");
            
        }
    }
}