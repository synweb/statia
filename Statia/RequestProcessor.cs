﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using NLog;

namespace Statia
{
    public class RequestProcessor
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        public async Task WriteNotFoundResponse(HttpContext context)
        {
            context.Response.StatusCode = 404;
            await context.Response.WriteAsync($"404. Not found. Statia server v{Program.Version}.");
        }

        private void AddCacheHeaders(HttpContext context)
        {
            const int durationInSeconds = 60 * 60 * 24;
            context.Response.Headers[HeaderNames.CacheControl] =
                "public,max-age=" + durationInSeconds; 
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

        public async Task<int?> ProcessRequest(HttpContext context)
        {
            // returns http status code
            AddCacheHeaders(context);
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var requestUrl = context.Request.Path.Value.ToLower();
            if (requestUrl.Length > 1 && requestUrl.EndsWith('/'))
            {
                requestUrl = requestUrl.Substring(0, requestUrl.Length - 1);
            }

            if (Regex.IsMatch(requestUrl, @"index\..+\.html"))
            {
                return 404;
            }
            var locale = GetRequestLocale(context);
            if (locale == string.Empty)
            {
                locale = null;
            }

            var pageCache = (PageCache) context.Items["pages"];
            bool hasLocalizedFile = pageCache.ContainsKey((locale, requestUrl));
            if (hasLocalizedFile)
            {
                var code = await WriteResponse(context, locale, requestUrl, stopwatch);
                return code;
            }
            bool hasGlobalFile = pageCache.ContainsKey((null, requestUrl));
            if (hasGlobalFile)
            {
                var code = await WriteResponse(context, null, requestUrl, stopwatch);
                return code;
            }
            return null;
        }

        private async Task<int> WriteResponse(HttpContext context, string locale, string requestUrl, Stopwatch stopwatch)
        {
            var pageCache = (PageCache) context.Items["pages"];
            int responseStatusCode;
            long time;
            try
            {
                responseStatusCode = 200;
                var responseText = pageCache[(locale, requestUrl)];
                time = stopwatch.ElapsedMilliseconds;
                await context.Response.WriteAsync(responseText);
            }
            catch (Exception e)
            {
                _logger.Error(e);
                responseStatusCode = 500;
                time = stopwatch.ElapsedMilliseconds;
            }
            stopwatch.Stop();
            string logMsg =
                $"{context.Connection.RemoteIpAddress}:{context.Connection.RemotePort}\t\t{responseStatusCode}\t\t{requestUrl}\t\t{time} ms";
            _logger.Info(logMsg);
            return responseStatusCode;
        }
    }
}