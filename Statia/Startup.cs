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

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            Preload();
            app.Use(async (context, next) =>
            {
                context.Items["pages"] = _urlPageCache;
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
                if (item == null)
                {
                    await _requestProcessor.WriteNotFoundResponse(context);
                    return;
                }
                int code = (int) item;
                context.Response.StatusCode = code;
                if (code==404)
                {
                }
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