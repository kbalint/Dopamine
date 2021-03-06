﻿using Dopamine.Core.Database.Entities;
using Dopamine.Core.Logging;
using System;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Collections.Concurrent;
using Dopamine.Core.IO;
using Dopamine.Core.Base;
using Dopamine.Core.Database.Repositories.Interfaces;

namespace Dopamine.Core.Database.Repositories
{
    public class FolderRepository : IFolderRepository
    {
        #region Variables
        private SQLiteConnectionFactory factory;
        #endregion

        #region Construction
        public FolderRepository()
        {
            this.factory = new SQLiteConnectionFactory();
        }
        #endregion

        #region IFolderRepository
        public async Task<AddFolderResult> AddFolderAsync(Folder folder)
        {
            AddFolderResult result = AddFolderResult.Success;

            await Task.Run(() =>
            {
                try
                {
                    using (var conn = this.factory.GetConnection())
                    {
                        try
                        {
                            if (!conn.Table<Folder>().Select((t) => t.Path.ToLower()).ToList().Contains(folder.Path.ToLower()))
                            {
                                conn.Insert(folder);
                                LogClient.Instance.Logger.Info("Added the Folder {0}", folder.Path);
                            }
                            else
                            {
                                LogClient.Instance.Logger.Info("Didn't add the Folder {0} because it is already in the database", folder.Path);
                                result = AddFolderResult.Duplicate;
                            }
                        }
                        catch (Exception ex)
                        {
                            LogClient.Instance.Logger.Error("Could not add the Folder {0}. Exception: {1}", folder.Path, ex.Message);
                            result = AddFolderResult.Error;
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogClient.Instance.Logger.Error("Could not connect to the database. Exception: {0}", ex.Message);
                }
            });

            return result;
        }

        public async Task<RemoveFolderResult> RemoveFolderAsync(string path)
        {
            RemoveFolderResult result = RemoveFolderResult.Success;

            await Task.Run(() =>
            {
                try
                {
                    using (var conn = this.factory.GetConnection())
                    {
                        try
                        {
                            var obsoleteFolder = conn.Table<Folder>().Where((s) => s.Path.ToLower().Equals(path.ToLower())).Select((s) => s).FirstOrDefault();

                            if (obsoleteFolder != null)
                            {
                                conn.Delete(obsoleteFolder);
                                LogClient.Instance.Logger.Info("Removed the Folder {0}", path);
                            }
                        }
                        catch (Exception ex)
                        {
                            LogClient.Instance.Logger.Error("Could not remove the Folder {0}. Exception: {1}", path, ex.Message);
                            result = RemoveFolderResult.Error;
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogClient.Instance.Logger.Error("Could not connect to the database. Exception: {0}", ex.Message);
                }
            });

            return result;
        }

        public async Task<List<Folder>> GetFoldersAsync()
        {
            var allFolders = new List<Folder>();

            await Task.Run(() =>
            {
                try
                {
                    using (var conn = this.factory.GetConnection())
                    {
                        try
                        {
                            allFolders = conn.Table<Folder>().Select((s) => s).ToList();
                        }
                        catch (Exception ex)
                        {
                            LogClient.Instance.Logger.Error("Could not get all the Folders. Exception: {0}", ex.Message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogClient.Instance.Logger.Error("Could not connect to the database. Exception: {0}", ex.Message);
                }

            });

            return allFolders;
        }

        public async Task<List<Tuple<long, string, long>>> GetPathsAsync()
        {
            var diskPaths = new List<Tuple<long, string, long>>();
            List<Folder> folders = await this.GetFoldersAsync();

            await Task.Run(() =>
            {
                // Recursively get all the files in the collection folders
                foreach (Folder fol in folders)
                {
                    if (Directory.Exists(fol.Path))
                    {
                        var paths = new List<string>();

                        try
                        {
                            // Create a queue to hold exceptions that have occurred while scanning the directory tree
                            var recurseExceptions = new ConcurrentQueue<Exception>();

                            // Get all audio files recursively
                            FileOperations.DirectoryRecursiveGetFiles(fol.Path, paths, FileFormats.SupportedMediaExtensions, recurseExceptions);

                            if (recurseExceptions.Count > 0)
                            {
                                foreach (Exception recurseException in recurseExceptions)
                                {
                                    LogClient.Instance.Logger.Error("Error while recursively getting files/folders. Exception: {0}", recurseException.ToString());
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            LogClient.Instance.Logger.Error("Could not iterate through the subdirectories of folder '{0}'. Exception: {1}", fol, ex.Message);
                        }

                        foreach (string path in paths)
                        {
                            try
                            {
                                diskPaths.Add(new Tuple<long, string, long>(fol.FolderID, path, FileOperations.GetDateModified(path)));
                            }
                            catch (Exception ex)
                            {
                                LogClient.Instance.Logger.Error("Could not add path '{0}' to the list of folder paths, while processing folder '{1}'. Exception: {2}", path, fol, ex.Message);
                            }
                        }
                    }
                }
            });

            return diskPaths;
        }

        public async Task UpdateFoldersAsync(IList<Folder> folders)
        {
            await Task.Run(() =>
            {
                try
                {
                    using (var conn = this.factory.GetConnection())
                    {
                        try
                        {
                            foreach (Folder fol in folders)
                            {
                                var dbFolder = conn.Table<Folder>().Select((f) => f).Where((f) => f.Path.ToLower().Equals(fol.Path.ToLower())).FirstOrDefault();

                                if (dbFolder != null)
                                {
                                    dbFolder.ShowInCollection = fol.ShowInCollection;
                                    conn.Update(dbFolder);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            LogClient.Instance.Logger.Error("Could not update the Folders. Exception: {0}", ex.Message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogClient.Instance.Logger.Error("Could not connect to the database. Exception: {0}", ex.Message);
                }
            });
        }
        #endregion
    }
}
