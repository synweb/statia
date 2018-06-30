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
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;
using NLog;

namespace Statia
{
    public class Startup
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            var applicationPartManager = new ApplicationPartManager();
            applicationPartManager.ApplicationParts.Add(new AssemblyPart(typeof(Startup).Assembly));
            services.Add(new ServiceDescriptor(typeof(ApplicationPartManager), applicationPartManager));
        }

        private string GetRequestLocale(HttpContext context)
        {
            string cookieLocaleValue;
            bool cookieLocaleExists = context.Request.Cookies.TryGetValue("loc", out cookieLocaleValue);
            if (cookieLocaleExists && !string.IsNullOrEmpty(cookieLocaleValue))
                return cookieLocaleValue;
            var contextLocaleString = context.Request.Headers["Accept-Language"].ToString();
            var result = contextLocaleString.Split(',').FirstOrDefault().Split('-').First().ToLower();
            return result;
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            Preload();
            app.Run(async (context) => { await ProcessRequest(context); });
        }

        private async Task ProcessRequest(HttpContext context)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var locale = GetRequestLocale(context);
            if (locale == string.Empty)
            {
                locale = null;
            }

            var requestUrl = context.Request.Path.Value.ToLower();
            bool hasLocalizedFile = _urlPageCache.ContainsKey((locale, requestUrl));
            if (hasLocalizedFile)
            {
                await WriteResponse(context, locale, requestUrl, stopwatch);
            }
            else
            {
                bool hasGlobalFile = _urlPageCache.ContainsKey((null, requestUrl));
                if (hasGlobalFile)
                {
                    await WriteResponse(context, null, requestUrl, stopwatch);
                }
                else
                {
                    context.Response.StatusCode = 404;
                    await context.Response.WriteAsync($"Not found. Statia server v{VERSION}.");
                    return;
                }
            }
        }

        private async Task WriteResponse(HttpContext context, string locale, string requestUrl, Stopwatch stopwatch)
        {
            try
            {
                var responseStatusCode = 200;
                context.Response.StatusCode = responseStatusCode;
                var responseText = _urlPageCache[(locale, requestUrl)];
                var isFile = !requestUrl.EndsWith(".html");
                if (isFile)
                {
                    const int durationInSeconds = 60 * 60 * 24;
                    context.Response.Headers[HeaderNames.CacheControl] =
                        "public,max-age=" + durationInSeconds; 
                }
                var writeTask = context.Response.WriteAsync(responseText);
                stopwatch.Stop();
                var time = stopwatch.ElapsedMilliseconds;
                var msg =
                    $"{context.Connection.RemoteIpAddress}:{context.Connection.RemotePort}\t\t{responseStatusCode}\t\t{requestUrl}\t\t{time} ms";
                _logger.Info(msg);
                await writeTask; 
            }
            catch (Exception e)
            {
                _logger.Error(e);
                context.Response.StatusCode = 500;
            }
        }

        private string _defaultLocale = "en";
        public const string VERSION = "0.1";

        /// <summary>
        /// [en:[relativeUrl:relativeUrl.html,...],...]
        /// </summary>
        private Dictionary<(string, string), string> _urlPageCache;

        private Loader _loader;
        
        private void Preload()
        {
            var sw = new Stopwatch();
            sw.Start();
            Console.WriteLine("Loading pages:");
            _loader = new Loader(Program.RootDirectory);
            _urlPageCache = _loader.Load();
            sw.Stop();
            Console.WriteLine($"Loaded in {sw.ElapsedMilliseconds} ms");
            
        }
    }
}