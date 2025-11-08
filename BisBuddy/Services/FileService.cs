using BisBuddy.Util;
using Dalamud.Plugin;
using Dalamud.Utility;
using System;
using System.IO;
using System.IO.Abstractions;
namespace BisBuddy.Services
{
    public class FileService(
        ITypedLogger<FileService> logger,
        IDalamudPluginInterface pluginInterface,
        IFileSystem fileSystem
        ) : IFileService
    {
        private readonly ITypedLogger<FileService> logger = logger;
        private readonly IDalamudPluginInterface pluginInterface = pluginInterface;
        private readonly IFileSystem fileSystem = fileSystem;
        private readonly IFile file = fileSystem.File;

        private string gearsetsDirectoryPath => Path.Combine(pluginInterface.ConfigDirectory.FullName, Constants.GearsetsDirectoryName);

        /// <summary>
        /// Get the path of the file that stores the gearset data for the provided character contentId
        /// Resolves to **/XIVLauncher/pluginConfigs/BisBuddy/gearsets/{<see cref="contentId"/>}.json
        /// </summary>
        /// <param name="contentId">The character local contentId to get the gearsets file path for</param>
        /// <returns>The gearsets path for the character</returns>
        private string getCharacterGearsetPath(ulong contentId) =>
            Path.Combine(gearsetsDirectoryPath, $"{contentId}.json");

        private void createGearsetsDirectory()
        {
            logger.Info("Creating gearsets directory");
            fileSystem.Directory.CreateDirectory(gearsetsDirectoryPath);
        }

        public FileSystemStream OpenReadConfigStream() =>
            file.OpenRead(pluginInterface.ConfigFile.FullName);

        public FileSystemStream OpenReadGearsetsStream(ulong contentId)
        {
            if (!fileSystem.Directory.Exists(gearsetsDirectoryPath))
                createGearsetsDirectory();
            return file.OpenRead(getCharacterGearsetPath(contentId));
        }

        public void WriteConfigString(string serializedConfigData)
        {
            try
            {
                FilesystemUtil.WriteAllTextSafe(pluginInterface.ConfigFile.FullName, serializedConfigData);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error writing configuration data");
            }
        }

        public void WriteGearsetsString(ulong contentId, string serializedGearsetsData)
        {
            try
            {
                if (!fileSystem.Directory.Exists(gearsetsDirectoryPath))
                    createGearsetsDirectory();
                FilesystemUtil.WriteAllTextSafe(getCharacterGearsetPath(contentId), serializedGearsetsData);
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error writing gearsets for contentId {contentId}");
            }
        }
    }

    public interface IFileService
    {
        /// <summary>
        /// Reads the configuration data from file
        /// </summary>
        /// <returns>The string representation of the configuration data</returns>
        public FileSystemStream OpenReadConfigStream();

        /// <summary>
        /// Reads the gearset data from file for the provided character contentId
        /// </summary>
        /// <param name="contentId">The content id of the character to load the gearset data from</param>
        /// <returns>The string representation of the gearset data</returns>
        public FileSystemStream OpenReadGearsetsStream(ulong contentId);

        /// <summary>
        /// Saves the provided configuration data to file synchronously
        /// </summary>
        /// <param name="serializedConfigData">The serialized configuration data</param>
        public void WriteConfigString(string serializedConfigData);

        /// <summary>
        /// Saves the provided gearset data to file for the provided character contentId synchronously
        /// </summary>
        /// <param name="contentId">The content id of the character to save the gearset data to</param>
        /// <param name="serializedGearsetsList">The serialized gearset data to save</param>
        public void WriteGearsetsString(ulong contentId, string serializedGearsetsList);
    }
}
