using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NLog;

namespace Statia
{
    public class Loader
    {
        public Loader(string contentRoot)
        {
            _contentRoot = contentRoot;
        }
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        /// <summary>
        /// [en:[relativeUrl:relativeUrl.html,...],...]
        /// </summary>
        private readonly Dictionary<(string,string), string> _urlPageCache = new
            Dictionary<(string,string), string>();

        private readonly string _contentRoot;

        private void LoadFile(string filePath)
        {
            var localeUrlPair = GetLocaleAndRelativeUrl(filePath);
            LoadPageToCache(localeUrlPair.Item1, localeUrlPair.Item2, filePath);
        }
        
        private void PreloadFolderRecursively(string path, string relativePathPrefix)
        {
            var files = Directory.EnumerateFiles(path).ToArray();
            if (files.Length == 0)
            {
                return;
            }
            foreach (var filePath in files)
            {
                LoadFile(filePath);
            }
            var dirs = Directory.EnumerateDirectories(path).Where(x => !x.StartsWith("."));
            foreach (var dir in dirs)
            {
                var childPrefix =
                    $"{relativePathPrefix}{GetFileOrDirectoryName(dir)}/";
                PreloadFolderRecursively(dir, childPrefix);
            }
        }

        private string GetFileOrDirectoryName(string fullPath)
        {
            return fullPath.Split(new[] {'/', '\\'}, StringSplitOptions.RemoveEmptyEntries).Last();
        }

        private void LoadPageToCache(string locale, string url, string filePath)
        {
            Console.WriteLine($"{locale??"default"}\t\t:\t\t{url}\t\t:\t\t{filePath}");
            var content = File.ReadAllText(filePath);
            _urlPageCache.Add((locale, url),  content);
        }
        
        public (string,string) GetLocaleAndRelativeUrl(string relativePath)
        {
            var match = Regex.Match(relativePath, @"^(\/.+)\/(.+?)(\.\w{0,2})?(\.\w+)$");
            if (!match.Success)
                return (null, null);
            string preFilePath = match.Groups[1].Value;
            string preUrlPath = $"{preFilePath.Substring(_contentRoot.Length)}/"; // get rid of absolute path
            var filenameWithoutExtensionAndLocale = match.Groups[2].Value;
            var localeMatch = match.Groups[3].Value;
            var extensionMatch = match.Groups[4].Value;
            var isHtml = extensionMatch.Equals(".html");
            string relativeUrl;
            if (isHtml)
            {
                relativeUrl = filenameWithoutExtensionAndLocale == "index" ? preUrlPath : $"{preUrlPath}{filenameWithoutExtensionAndLocale}";
                if (relativeUrl.Length > 1 && relativeUrl.EndsWith('/'))
                {
                    relativeUrl = relativeUrl.Substring(0, relativeUrl.Length - 1);
                }
            }
            else
            {
                // full file name
                relativeUrl = $"{preUrlPath}{filenameWithoutExtensionAndLocale}{localeMatch}{extensionMatch}";
            }
            var locale = string.IsNullOrEmpty(localeMatch) ? null : localeMatch.Substring(1); // dot is the first symbol
            return (locale, relativeUrl);
        }

        public Dictionary<(string, string), string> Load()
        {
            PreloadFolderRecursively(_contentRoot, "/");
            SetupWatcher(_contentRoot);
            return _urlPageCache;
        }

        private void SetupWatcher(string rootDirectory)
        {
            var fw = new FileSystemWatcher(Program.RootDirectory);
            fw.Created += WatcherEvent;
            fw.Deleted += WatcherEvent;
            fw.Renamed += WatcherEvent;
            fw.Changed += WatcherEvent;
            fw.EnableRaisingEvents = true;
        }

        private void WatcherEvent(object sender, FileSystemEventArgs e)
        {
            var name = GetFileOrDirectoryName(e.FullPath);
            if (name.StartsWith("."))
            {
                return;
            }
            var relativePath = GetFileOrDirectoryName(e.FullPath);
            _logger.Trace($"{relativePath} {e.ChangeType}");
            switch (e.ChangeType)
            {
                case WatcherChangeTypes.Deleted:
                    break;
            }
            
        }
    }
}