using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NLog.Web;

namespace Statia
{
    public class Program
    {
        private static int _port = 57471;

        public static string RootDirectory { get; private set; }
        public static string Version = "0.1";
        
        /// <summary>
        /// using: satia /var/www/html -p 8080
        /// </summary>
        /// <param name="args"></param>
        public static void Main(string[] args)
        {
            string defaultRootDir = $"{AppDomain.CurrentDomain.BaseDirectory}wwwroot";
            if (args.Length > 0 && Directory.Exists(args[0]))
            {
                RootDirectory = args[0];
            }
            else
            {
                RootDirectory = defaultRootDir;
            }
            
            for(int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                switch (arg)
                {
                        case "-p":
                        case "--port":
                            string portArg = args[i + 1];
                            _port = int.Parse(portArg);
                            break;
                }
                
            }
            CreateWebHostBuilder(args)
                .Build()
                .Run();
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost
                .CreateDefaultBuilder(args)
                .UseStartup<Startup>()
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
                })
                .UseUrls($"http://localhost:{_port}")
                .UseNLog();
    }
}