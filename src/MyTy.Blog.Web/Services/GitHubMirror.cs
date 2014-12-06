﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using MyTy.Blog.Web.Models;
using MyTy.Blog.Web.Models.Github;
using RestSharp;

namespace MyTy.Blog.Web.Services
{
	public class GitHubMirror
	{
		readonly string gitHubBaseDir;
		readonly string localDir;		
		readonly GitHubAPI gitHubAPI;

		public GitHubMirror(string gitHubOwner, string gitHubRepo, string gitHubBaseDir, string localDir, string oAuthToken)
		{
			this.gitHubBaseDir = gitHubBaseDir;
			this.localDir = localDir;
			this.gitHubAPI = new GitHubAPI(gitHubOwner, gitHubRepo, "myty-blog-engine", oAuthToken);
		}

		public async Task<GitHubMirrorSynchronizeResult> SynchronizeAsync(bool okToDeleteFiles = true)
		{
			var result = new GitHubMirrorSynchronizeResult();

			try {

				var pagesContentResult = await gitHubAPI.GetContent(gitHubBaseDir);

				var treeContents = await Task.WhenAll(pagesContentResult.Where(p => p.type == "dir").Select(async p => {
					var treeResults = await gitHubAPI.GetTreeRecursively(p.sha);
					return new { 
						p.path,
						treeResults 
					};
				}));

				var filesPathSha = pagesContentResult.Where(p => p.type == "file").Select(f => new {
					path = f.path.Replace(gitHubBaseDir + "/", ""),
					sha = f.sha
				}).Concat(treeContents.SelectMany(t => t.treeResults.tree.Where(p => p.type == "blob").Select(p => new {
					path = t.path.Replace(gitHubBaseDir + "/", "") + "/" + p.path,
					sha = p.sha
				})));

				var remoteFilesSynced = (await Task.WhenAll(filesPathSha.Select(async f => {
					var getBlobTask = gitHubAPI.GetBlob(f.sha);

					var status = "";
					var localFileInfo = new System.IO.FileInfo(localDir + "\\" + f.path.Replace("/", "\\"));

					var blob = await getBlobTask;

					var fileContents = (blob.encoding == "base64") ?
						Encoding.UTF8.GetString(Convert.FromBase64String(blob.content)) :
						blob.content;


					if (!localFileInfo.Exists) {
						status = "added";
						localFileInfo.Directory.Create();
						File.WriteAllText(localFileInfo.FullName, fileContents);
					} else if (!File.ReadAllText(localFileInfo.FullName).Equals(fileContents)) {
						status = "updated";
						File.WriteAllText(localFileInfo.FullName, fileContents);
					}

					return new {
						localPath = localFileInfo.FullName,
						status
					};
				}))).ToArray();

				result.FilesUpdated = remoteFilesSynced
					.Where(f => f.status == "updated")
					.Select(f => f.localPath)
					.ToArray();

				result.FilesAdded = remoteFilesSynced
					.Where(f => f.status == "added")
					.Select(f => f.localPath)
					.ToArray();

				result.FilesDeleted = Directory
					.EnumerateFiles(localDir, "*", SearchOption.AllDirectories)
					.Where(f => !remoteFilesSynced.Any(s => s.localPath == f))
					.Select(f => {
						if (okToDeleteFiles) { File.Delete(f); }
						return f;
					})
					.ToArray();

			} catch (Exception ex) {
				throw ex;
			}

			return result;
		}
	}

	public class GitHubMirrorSynchronizeResult
	{
		public GitHubMirrorSynchronizeResult()
		{			
			FilesUpdated = Enumerable.Empty<string>();
			FilesAdded = Enumerable.Empty<string>();
			FilesDeleted = Enumerable.Empty<string>();
		} 

		public IEnumerable<string> FilesAdded { get; set; }
		public IEnumerable<string> FilesUpdated { get; set; }
		public IEnumerable<string> FilesDeleted { get; set; }
	}
}