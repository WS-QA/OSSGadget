﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;

namespace Microsoft.CST.OpenSource.Shared
{
    class MavenProjectManager : BaseProjectManager
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public static string ENV_MAVEN_ENDPOINT = "https://repo1.maven.org/maven2";

        /// <summary>
        /// Download one Maven package and extract it to the target directory.
        /// </summary>
        /// <param name="purl">Package URL of the package to download.</param>
        /// <returns>n/a</returns>
        public override async Task<string> DownloadVersion(PackageURL purl, bool doExtract = true)
        {
            Logger.Trace("DownloadVersion {0}", purl?.ToString());

            var packageNamespace = purl?.Namespace?.Replace('.', '/');
            var packageName = purl?.Name;
            var packageVersion = purl?.Version;
            string downloadedPath = null;

            if (string.IsNullOrWhiteSpace(packageName) || string.IsNullOrWhiteSpace(packageVersion))
            {
                Logger.Error("Unable to download [{0} {1}]. Both must be defined.", packageName, packageVersion);
                return null;
            }

            try
            {
                var suffixes = new string[] { "-javadoc", "-sources", "" };
                foreach (var suffix in suffixes)
                {
                    var url = $"{ENV_MAVEN_ENDPOINT}/{packageNamespace}/{packageName}/{packageVersion}/{packageName}-{packageVersion}{suffix}.jar";
                    var result = await WebClient.GetAsync(url);
                    result.EnsureSuccessStatusCode();
                    Logger.Debug($"Downloading {purl}...");

                    var targetName = $"maven-{purl.Namespace}/{packageName}{suffix}@{packageVersion}";
                    if (doExtract)
                    {
                        downloadedPath = await ExtractArchive(targetName, await result.Content.ReadAsByteArrayAsync());
                    }
                    else
                    {
                        await File.WriteAllBytesAsync(targetName, await result.Content.ReadAsByteArrayAsync());
                        downloadedPath = targetName;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error downloading Maven package: {ex.Message}");
                downloadedPath = null;
            }
            return downloadedPath;
        }

        public override async Task<IEnumerable<string>> EnumerateVersions(PackageURL purl)
        {
            Logger.Trace("EnumerateVersions {0}", purl?.ToString());
            if (purl == null)
            {
                return new List<string>();
            }
            try
            {
                var packageNamespace = purl.Namespace.Replace('.', '/');
                var packageName = purl.Name;
                var content = await GetHttpStringCache($"{ENV_MAVEN_ENDPOINT}/{packageNamespace}/{packageName}/maven-metadata.xml");
                var versionList = new List<string>();

                var doc = new XmlDocument();
                doc.LoadXml(content);
                foreach (XmlNode versionObject in doc.GetElementsByTagName("version"))
                {
                    Logger.Debug("Identified {0} version {1}.", packageName, versionObject.InnerText);
                    versionList.Add(versionObject.InnerText);
                }
                return SortVersions(versionList.Distinct());
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error enumerating Maven packages: {ex.Message}");
                return Array.Empty<string>();
            }
        }
        public override async Task<string> GetMetadata(PackageURL purl)
        {
            try
            {
                var packageNamespace = purl.Namespace.Replace('.', '/');
                var packageName = purl.Name;
                if (purl.Version == null)
                {
                    foreach (var version in await EnumerateVersions(purl))
                    {
                        return await GetHttpStringCache($"{ENV_MAVEN_ENDPOINT}/{packageNamespace}/{packageName}/{version}/{packageName}-{version}.pom");
                    }
                    throw new Exception("No version specified and unable to enumerate.");
                }
                else
                {
                    var version = purl.Version;
                    return await GetHttpStringCache($"{ENV_MAVEN_ENDPOINT}/{packageNamespace}/{packageName}/{version}/{packageName}-{version}.pom");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error fetching Maven metadata: {ex.Message}");
                return null;
            }
        }
    }
}

// test case: https://repo1.maven.org/maven2/glass/phil/auto/moshi/auto-moshi-annotations/0.2.0/auto-moshi-annotations-0.2.0.pom

