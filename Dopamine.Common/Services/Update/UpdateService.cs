﻿using Dopamine.Core.Base;
using Dopamine.Core.Helpers;
using Dopamine.Core.IO;
using Dopamine.Core.Logging;
using Dopamine.Core.Settings;
using Ionic.Zip;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Timers;
using System.Xml.Linq;

namespace Dopamine.Common.Services.Update
{
    public class UpdateService : IUpdateService
    {
        #region Variables
        // Directory
        private string updatesSubDirectory;
        // Flags
        private bool canCheckForUpdates;
        private bool checkingForUpdates;
        private bool checkForPreReleases;
        private bool automaticDownload;

        // Timer
        private Timer checkNewVersionTimer = new Timer();

        // WebClient
        private WebClient downloadClient;
        #endregion

        #region Construction
        public UpdateService()
        {
            this.checkNewVersionTimer.Elapsed += new ElapsedEventHandler(this.CheckNewVersionTimerHandler);

            this.updatesSubDirectory = Path.Combine(XmlSettingsClient.Instance.ApplicationFolder, ApplicationPaths.UpdatesSubDirectory);
            this.canCheckForUpdates = false;
        }
        #endregion

        #region Private
        private VersionInfo XElement2VersionInfo(XElement element)
        {
            try
            {
                Configuration configuration = (Configuration)Convert.ToInt32(element.Attribute("Configuration").Value);

                string[] pieces = element.Value.Split('.');

                return new VersionInfo
                {
                    Version = new Version(Convert.ToInt32(pieces[0]), Convert.ToInt32(pieces[1]), Convert.ToInt32(pieces[2]), Convert.ToInt32(pieces[3])),
                    Configuration = configuration
                };
            }
            catch (Exception)
            {
                return new VersionInfo
                {
                    Version = new Version(0, 0, 0, 0),
                    Configuration = Configuration.Debug
                };
            }
        }

        private bool IsValidVersion(Version version)
        {
            if (version.Major == 0 & version.Minor == 0 & version.Build == 0 & version.Revision == 0)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        private async Task<VersionInfo> GetLatestOnlineVersionAsync()
        {
            // Dummy version. If the version remains 0.0.0.0, there is no update available
            var onlineVersion = new VersionInfo();

            await Task.Run(() =>
            {
                try
                {
                    // Download the version information web page from the Internet
                    // Random part required to make sure that we don't download a cached versions file.
                    WebRequest request = WebRequest.Create(string.Format("{0}?random={1}", UpdateInformation.VersionFileLink, DateTime.Now.Ticks.ToString()));
                    WebResponse response = request.GetResponse();
                    StreamReader reader = new StreamReader(response.GetResponseStream());
                    string str = reader.ReadToEnd();

                    // Create an XML from the downloaded file
                    XDocument doc = default(XDocument);
                    doc = XDocument.Parse(str);

                    // Query the XML file for only result which match this application name
                    var versionXElements = (from a in doc.Element("Applications").Elements("Application")
                                            from v in a.Elements("Version")
                                            where a.Attribute("Name").Value.ToLower().Equals(ProductInformation.ApplicationDisplayName.ToLower())
                                            orderby v.Value
                                            select v).ToList();

                    var foundPreReleases = new List<VersionInfo>();
                    var foundReleases = new List<VersionInfo>();

                    // Make lists of all found PreReleases and Releases
                    foreach (XElement el in versionXElements)
                    {
                        VersionInfo vi = this.XElement2VersionInfo(el);

                        if (this.IsValidVersion(vi.Version))
                        {
                            if (vi.Configuration == Configuration.Debug)
                            {
                                foundPreReleases.Add(vi);
                            }
                            else if (vi.Configuration == Configuration.Release)
                            {
                                foundReleases.Add(vi);
                            }
                        }
                    }

                    // Create 2 dummy versions to compare during the first round
                    var lastestPreRelease = new VersionInfo();
                    var lastestRelease = new VersionInfo();

                    // Find the latest PreRelease
                    foreach (VersionInfo foundPreRelease in foundPreReleases)
                    {
                        if (lastestPreRelease.IsOlder(foundPreRelease))
                        {
                            lastestPreRelease = foundPreRelease;
                        }
                    }

                    // Find the latest Release
                    foreach (VersionInfo foundRelease in foundReleases)
                    {
                        if (lastestRelease.IsOlder(foundRelease))
                        {
                            lastestRelease = foundRelease;
                        }
                    }

                    // Then only check for new PreReleases if the user specified to check for PreReleases

                    if (this.checkForPreReleases)
                    {
                        if (lastestPreRelease.IsOlder(lastestRelease))
                        {
                            onlineVersion = lastestRelease;
                        }
                        else
                        {
                            onlineVersion = lastestPreRelease;
                        }
                    }
                    else
                    {
                        onlineVersion = lastestRelease;
                    }

                }
                catch (Exception ex)
                {
                    LogClient.Instance.Logger.Error("Update check: could not retrieve online version information. Exception: {0}", ex.Message);
                }
            });

            return onlineVersion;
        }

        private async Task<bool> TryCreateUpdatesSubDirectoryAsync()
        {
            bool createSuccessful = false;

            await Task.Run(() =>
            {
                try
                {
                    // If the Updates subdirectory doesn't exist, create it
                    if (!Directory.Exists(this.updatesSubDirectory))
                    {
                        Directory.CreateDirectory(this.updatesSubDirectory);
                    }

                    createSuccessful = true;
                }
                catch (Exception ex)
                {
                    LogClient.Instance.Logger.Error("Update check: could not create the Updates subdirectory. Exception: {0}", ex.Message);
                    createSuccessful = false;
                }
            });

            return createSuccessful;
        }

        private void TryCleanUpdatesSubDirectory()
        {
            try
            {
                foreach (System.IO.FileInfo fi in new DirectoryInfo(this.updatesSubDirectory).GetFiles("*.*"))
                {
                    try
                    {
                        fi.Delete();
                    }
                    catch (Exception ex)
                    {
                        LogClient.Instance.Logger.Error("Update check: could not delete the file {0}. Exception: {1}", fi.FullName, ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                LogClient.Instance.Logger.Error("Update check: Error while cleaning the Updates subdirectory. Exception: {0}", ex.Message);
            }
        }

        private async Task<OperationResult> DownloadAsync(VersionInfo latestOnlineVersion, string packageToDownload)
        {
            var operationResult = new OperationResult();

            // Try to create the Updates subdirectory. If this fails: return.
            if (!await this.TryCreateUpdatesSubDirectoryAsync())
            {
                operationResult.Result = false;
                return operationResult;
            }

            // Delete all files from the Updates subdirectory. If this fails, just continue.
            this.TryCleanUpdatesSubDirectory();

            try
            {
                string downloadLink = Path.Combine(UpdateInformation.ReleaseAutomaticDownloadLink, Path.GetFileName(packageToDownload));

                if (latestOnlineVersion.Configuration == Configuration.Debug)
                {
                    downloadLink = Path.Combine(UpdateInformation.PreReleaseAutomaticDownloadLink, Path.GetFileName(packageToDownload));
                }

                this.downloadClient = new WebClient();

                LogClient.Instance.Logger.Info("Update check: downloading file '{0}'", downloadLink);

                await this.downloadClient.DownloadFileTaskAsync(new Uri(downloadLink), packageToDownload + ".part");

                operationResult.Result = true;
            }
            catch (Exception ex)
            {
                operationResult.Result = false;
                operationResult.AddMessage(ex.Message);
            }

            return operationResult;
        }

        private async Task<OperationResult> ProcessDownloadedPackageAsync()
        {
            var operationResult = new OperationResult();

            await Task.Run(() =>
            {
                // Gets the files with extension ".part"
                FileInfo[] partFiles = new DirectoryInfo(this.updatesSubDirectory).GetFiles("*.part");

                // Get the most recent file with extension ".part"
                FileInfo lastPartFile = partFiles.OrderByDescending(pf => pf.CreationTime).FirstOrDefault();
                string lastPackageFile = lastPartFile.FullName.Replace(".part", "");

                // Remove the ".part" extension
                if (lastPartFile != null)
                {
                    System.IO.File.Move(lastPartFile.FullName, lastPackageFile);
                }

                // Extract
                operationResult = this.ExtractDownloadedPackage(lastPackageFile);
            });

            return operationResult;
        }

        private bool IsDownloadedPackageAvailable(string package)
        {
            FileInfo fi = new FileInfo(package);

            if (fi.Exists && fi.Length > 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private OperationResult ExtractDownloadedPackage(string package)
        {
            var operationResult = new OperationResult();
            FileInfo fi = new FileInfo(package);
            string destinationPath = Path.Combine(this.updatesSubDirectory, Path.GetFileNameWithoutExtension(package));

            try
            {
                // Extract
                using (ZipFile zip = ZipFile.Read(fi.FullName))
                {
                    zip.ExtractAll(destinationPath, ExtractExistingFileAction.OverwriteSilently);
                }

                operationResult.Result = true;
            }
            catch (Exception ex)
            {
                operationResult.Result = false;
                operationResult.AddMessage(ex.Message);
            }

            return operationResult;
        }

        private async Task CheckForUpdatesAsync()
        {
            // Indicate for the rest of the class that we are checking for updates
            // -------------------------------------------------------------------
            this.checkingForUpdates = true;

            // We start checking for updates: stop the timer
            // ---------------------------------------------
            this.checkNewVersionTimer.Stop();

            // Get the current version
            // -----------------------
            var currentVersion = new VersionInfo { Version = ProductInformation.AssemblyVersion };
            LogClient.Instance.Logger.Info("Update check: current version = {0}.{1}.{2}.{3}", currentVersion.Version.Major, currentVersion.Version.Minor, currentVersion.Version.Build, currentVersion.Version.Revision);

            // Get the latest version online
            // -----------------------------
            if (!this.canCheckForUpdates) return; // Stop here if the update check was disabled while we were running
            VersionInfo latestOnlineVersion = await this.GetLatestOnlineVersionAsync();
            LogClient.Instance.Logger.Info("Update check: latest online version = {0}.{1}.{2}.{3}", latestOnlineVersion.Version.Major, latestOnlineVersion.Version.Minor, latestOnlineVersion.Version.Build, latestOnlineVersion.Version.Revision);

            // Compare the versions
            // --------------------
            if (currentVersion.IsOlder(latestOnlineVersion))
            {
                if (this.automaticDownload)
                {
                    // Automatic download is enabled
                    // -----------------------------

                    // Define the name of the file to which we will download the update
                    string updatePackageExtractedDirectoryFullPath = Path.Combine(this.updatesSubDirectory, PackagingInformation.GetPackageFileName(latestOnlineVersion));
                    string updatePackageDownloadedFileFullPath = Path.Combine(this.updatesSubDirectory, PackagingInformation.GetPackageFileName(latestOnlineVersion) + PackagingInformation.GetUpdatePackageFileExtension());

                    // Check if there is a directory with the name of the update package: 
                    // that means the file was already downloaded and extracted.
                    if (Directory.Exists(updatePackageExtractedDirectoryFullPath))
                    {

                        // The folder exists, that means that the new version was already extracted previously.
                        // Raise an event that a new version is available for installation.
                        if (this.canCheckForUpdates) this.NewDownloadedVersionAvailable(latestOnlineVersion, updatePackageExtractedDirectoryFullPath);
                    }
                    else
                    {
                        // Check if there is a package with the name of the update package: that would mean the update was already downloaded.
                        if (!this.IsDownloadedPackageAvailable(updatePackageDownloadedFileFullPath))
                        {
                            // No package available yet: download it.
                            OperationResult downloadResult = await this.DownloadAsync(latestOnlineVersion, updatePackageDownloadedFileFullPath);

                            if (downloadResult.Result)
                            {
                                OperationResult processResult = await this.ProcessDownloadedPackageAsync();

                                if (processResult.Result)
                                {
                                    // Processing the downloaded file was successful. Raise an event that a new version is available for installation.
                                    if (this.canCheckForUpdates) this.NewDownloadedVersionAvailable(latestOnlineVersion, updatePackageExtractedDirectoryFullPath);
                                }
                                else
                                {
                                    // Processing the downloaded file failed. Log the failure reason.
                                    LogClient.Instance.Logger.Error("Update check: could not process downloaded files. User is notified that there is a new version online. Exception: {0}", processResult.GetFirstMessage());

                                    // Raise an event that there is a new version available online.
                                    if (this.canCheckForUpdates) this.NewOnlineVersionAvailable(latestOnlineVersion);
                                }
                            }
                            else
                            {
                                // Downloading failed: log the failure reason.
                                LogClient.Instance.Logger.Error("Update check: could not download the file. Exception: {0}", downloadResult.GetFirstMessage());
                            }
                        }
                        else
                        {
                            OperationResult extractResult = this.ExtractDownloadedPackage(updatePackageDownloadedFileFullPath);

                            if (extractResult.Result)
                            {
                                // Extracting was successful. Raise an event that a new version is available for installation.
                                if (this.canCheckForUpdates) this.NewDownloadedVersionAvailable(latestOnlineVersion, updatePackageExtractedDirectoryFullPath);
                            }
                            else
                            {
                                // Extracting failed: log the failure reason.
                                LogClient.Instance.Logger.Error("Update check: could not extract the package. Exception: {0}", extractResult.GetFirstMessage());
                            }
                        }
                    }
                }
                else
                {
                    // Automatic download is not enabled
                    // ---------------------------------

                    // Raise an event that a New version Is available for download
                    if (this.canCheckForUpdates) this.NewOnlineVersionAvailable(latestOnlineVersion);
                }
            }
            else
            {
                this.NoNewVersionAvailable(latestOnlineVersion);
                LogClient.Instance.Logger.Info("Update check: no newer version was found.");
            }

            // Indicate for the rest of the class that we have finished checking for updates
            // -----------------------------------------------------------------------------
            this.checkingForUpdates = false;

            // We're finished checking for updates: start the timer
            // ----------------------------------------------------
            this.checkNewVersionTimer.Start();
        }
        #endregion

        #region IUpdateService
        public void EnableUpdateCheck()
        {
            // Log that we start checking for updates
            LogClient.Instance.Logger.Info("Update check: checking for updates. AlsoCheckForPreReleases = {0}", this.checkForPreReleases);

            // We can check for updates
            this.canCheckForUpdates = true;

            // Set the timer interval based on update settings
            this.checkNewVersionTimer.Interval = TimeSpan.FromSeconds(Constants.UpdateCheckIntervalSeconds).TotalMilliseconds;

            // Set flags based on update settings
            this.automaticDownload = XmlSettingsClient.Instance.Get<bool>("Updates", "AutomaticDownload") & !XmlSettingsClient.Instance.BaseGet<bool>("Application", "IsPortable");
            this.checkForPreReleases = XmlSettingsClient.Instance.Get<bool>("Updates", "AlsoCheckForPreReleases");

            // Actual update check. Don't await, just run async. (Stops the timer when starting and starts the timer again when ready)
            if (!this.checkingForUpdates) this.CheckForUpdatesAsync();
        }

        public void DisableUpdateCheck()
        {
            this.canCheckForUpdates = false;
            this.checkNewVersionTimer.Stop();

            this.UpdateCheckDisabled(this, null);
        }
        #endregion

        #region Events
        public event Action<VersionInfo, string> NewDownloadedVersionAvailable = delegate { };
        public event Action<VersionInfo> NewOnlineVersionAvailable = delegate { };
        public event Action<VersionInfo> NoNewVersionAvailable = delegate { };
        public event EventHandler UpdateCheckDisabled = delegate { };
        #endregion

        #region Event Handlers
        public void CheckNewVersionTimerHandler(object sender, ElapsedEventArgs e)
        {
            // Actual update check. Don't await, just run async. (Stops the timer when starting and starts the timer again when ready)
            if (!this.checkingForUpdates) this.CheckForUpdatesAsync();
        }
        #endregion
    }
}
