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
using Microsoft.AspNetCore.Mvc.ModelBinding.Internal;
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

//        private void PrintResponse(HttpContext context)
//        {
//            
//        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            Preload();
            app.Run(async (context) =>
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
            });
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
        private Dictionary<(string,string), string> _urlPageCache = new
            Dictionary<(string,string), string>();
        
        

        private void LoadPageToCache(string locale, string url, string filePath)
        {
            Console.WriteLine($"{locale??"default"}\t\t:\t\t{url}\t\t:\t\t{filePath}");
            var content = File.ReadAllText(filePath);
            _urlPageCache.Add((locale, url),  content);
        }

        private void PreloadFolderRecursively(string path, string relativePathPrefix)
        {
            Console.WriteLine($"Going into {path}");
            var files = Directory.EnumerateFiles(path).ToArray();
            if (files.Length == 0)
            {
                return;
            }
            var filePaths = files.Select(x =>
                new
                {
                    FilePath = x,
                    Match = Regex.Match(x.Substring(Program.RootDirectory.Length), @"(\w+)(\.\w{0,2})?(\.\w+)$") // getting filename
                }).Where(x => x.Match.Success).ToList();
            foreach (var pair in filePaths)
            {
                var isHtml = pair.Match.Groups[3].Value.Equals(".html");
                string relativeUrl;
                if (isHtml)
                {
                    // filename without extension
                    var filenameWithoutExt = pair.Match.Groups[1].Value;
                    relativeUrl = filenameWithoutExt == "index" ? relativePathPrefix : $"{relativePathPrefix}{filenameWithoutExt}"; 
                }
                else
                {
                    // full file name
                    relativeUrl = $"{relativePathPrefix}{pair.Match.Value}";
                }

                
                var locale = pair.Match.Groups[2].Value;
                locale = string.IsNullOrEmpty(locale) ? null : locale.Substring(1); // dot is the first symbol

                LoadPageToCache(locale, relativeUrl, pair.FilePath);
            }
            var dirs = Directory.EnumerateDirectories(path).Where(x => !x.StartsWith("."));
            foreach (var dir in dirs)
            {
                var childPrefix =
                    $"{relativePathPrefix}{dir.Split(new[] {'/', '\\'}, StringSplitOptions.RemoveEmptyEntries).Last()}/";
                PreloadFolderRecursively(dir, childPrefix);
            }
        }
        
        private void Preload()
        {
            var sw = new Stopwatch();
            sw.Start();
            Console.WriteLine("Loading pages:");
            PreloadFolderRecursively(Program.RootDirectory, "/");
            sw.Stop();
            Console.WriteLine($"Loaded in {sw.ElapsedMilliseconds} ms");
//            var indexFilename = "index.html";
//            var indexPath = Path.Combine(_contentDir, indexFilename);
//            if (File.Exists(indexPath))
//            {
//                LoadPageToCache(null, "/", indexPath);
//            }
            
            
        }
    }
}