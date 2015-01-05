﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Hosting;
using Nancy;
using Newtonsoft.Json;

namespace Muse.Web
{
    public interface IApplicationConfiguration
    {
        bool CanRefresh(Request request);
        string BaseUrl { get; }
        string GitHubToken { get; }
        string DisqusShortName { get; }
        string SiteTitle { get; }
        GitHubDirectorySync PagesSync { get; }
        GitHubDirectorySync PostsSync { get; }
        GitHubDirectorySync[] OthersSync { get; }
    }

    public class ApplicationConfiguration : IApplicationConfiguration
    {
        private string refreshToken;
        private string baseUrl;
        private string gitHubToken;
        private string disqusShortName;
        private GitHubDirectorySync pagesSync;
        private GitHubDirectorySync postsSync;
        private GitHubDirectorySync[] othersSync;

        readonly FileSystemWatcher watcher = new FileSystemWatcher();

        public ApplicationConfiguration()
        {
            var environmentConfigFilePath = HostingEnvironment.MapPath("~/env.config.json");
            if (File.Exists(environmentConfigFilePath)) {
                ScanEnvironmentConfigFile(environmentConfigFilePath);
            }

            watcher.Path = Path.GetDirectoryName(environmentConfigFilePath);
            watcher.IncludeSubdirectories = false;
            watcher.NotifyFilter = NotifyFilters.Attributes |
                NotifyFilters.CreationTime |
                NotifyFilters.FileName |
                NotifyFilters.LastAccess |
                NotifyFilters.LastWrite |
                NotifyFilters.Size |
                NotifyFilters.Security;
            watcher.Filter = Path.GetFileName(environmentConfigFilePath);

            watcher.Changed += (sender, e) => {
                ScanEnvironmentConfigFile(e.FullPath);
            };

            watcher.Created += (sender, e) => {
                ScanEnvironmentConfigFile(e.FullPath);
            };

            watcher.EnableRaisingEvents = true;
        }

        private void ScanEnvironmentConfigFile(string environmentConfigFilePath)
        {
            var environmentConfig = JsonConvert.DeserializeObject<EnvironmentConfig>(
                File.ReadAllText(environmentConfigFilePath));

            pagesSync = ResolvePaths(environmentConfig.pagesSync);
            postsSync = ResolvePaths(environmentConfig.postsSync);
            if (environmentConfig.othersSync != null) {
                othersSync = environmentConfig.othersSync.Select(ResolvePaths).ToArray();
            }

            disqusShortName = environmentConfig.disqus_shortname;
            refreshToken = environmentConfig.refreshToken;
            baseUrl = environmentConfig.baseUrl;
            gitHubToken = environmentConfig.gitHubToken;
        }

        public bool CanRefresh(Request request)
        {
            string apiKey = request.Query["key"];

            return apiKey != null && apiKey.Equals(refreshToken);
        }

        public string BaseUrl
        {
            get { return baseUrl; }
        }

        public string GitHubToken
        {
            get { return gitHubToken; }
        }

        public string DisqusShortName
        {
            get { return disqusShortName; }
        }

        private string siteTitle = "Easily Amused";
        public string SiteTitle { get { return siteTitle; } }

        public GitHubDirectorySync PagesSync { get { return pagesSync; } }
        public GitHubDirectorySync PostsSync { get { return postsSync; } }
        public GitHubDirectorySync[] OthersSync { get { return othersSync; } }

        private GitHubDirectorySync ResolvePaths(GitHubDirectorySync dirSync)
        {
            if (dirSync.locaPath.StartsWith("~/")) {
                dirSync.locaPath = HostingEnvironment.MapPath(dirSync.locaPath);
            }
            return dirSync;
        }
    }

    public class EnvironmentConfig
    {
        public string refreshToken { get; set; }
        public string baseUrl { get; set; }
        public string gitHubToken { get; set; }
        public string disqus_shortname { get; set; }

        public GitHubDirectorySync pagesSync { get; set; }
        public GitHubDirectorySync postsSync { get; set; }
        public GitHubDirectorySync[] othersSync { get; set; }
    }

    public class GitHubDirectorySync
    {
        public string owner { get; set; }
        public string repo { get; set; }
        public string branch { get; set; }
        public string remotePath { get; set; }
        public string locaPath { get; set; }
    }
}
