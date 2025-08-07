using Dalamud.Plugin;
using System.IO;
using System.IO.Abstractions;
using System.Threading;
using System.Threading.Tasks;

namespace BisBuddy.Services
{
    public class FileService(
        IDalamudPluginInterface pluginInterface,
        IFileSystem fileSystem
        ) : IFileService
    {
        private readonly IDalamudPluginInterface pluginInterface = pluginInterface;
        private readonly IFileSystem fileSystem = fileSystem;
        private readonly IFile file = fileSystem.File;

        private readonly string gearsetsDirectoryName = "gearsets";

        private string gearsetsDirectoryPath => Path.Combine(pluginInterface.ConfigDirectory.FullName, gearsetsDirectoryName);

        /// <summary>
        /// Get the path of the file that stores the gearset data for the provided character contentId
        /// Resolves to **/XIVLauncher/pluginConfigs/BisBuddy/gearsets/{<see cref="contentId"/>}.json
        /// </summary>
        /// <param name="contentId">The character local contentId to get the gearsets file path for</param>
        /// <returns>The gearsets path for the character</returns>
        private string getCharacterGearsetPath(ulong contentId) =>
            Path.Combine(gearsetsDirectoryPath, $"{contentId}.json");

        private void createGearsetsDirectory() =>
            fileSystem.Directory.CreateDirectory(gearsetsDirectoryPath);

        public FileSystemStream OpenReadConfigStream() =>
            file.OpenRead(pluginInterface.ConfigFile.FullName);

        public FileSystemStream OpenReadGearsetsStream(ulong contentId)
        {
            if (!fileSystem.Directory.Exists(gearsetsDirectoryPath))
                createGearsetsDirectory();
            return file.OpenRead(getCharacterGearsetPath(contentId));
        }

        public Task WriteConfigAsync(string serializedConfigData, CancellationToken cancellationToken = default)
            => file.WriteAllTextAsync(pluginInterface.ConfigFile.FullName, serializedConfigData, cancellationToken);

        public Task WriteGearsetsAsync(ulong contentId, string serializedGearsetsData, CancellationToken cancellationToken = default)
            => file.WriteAllTextAsync(getCharacterGearsetPath(contentId), serializedGearsetsData, cancellationToken);
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
        /// Saves the provided configuration data to file
        /// </summary>
        /// <param name="serializedConfigData">The serialized configuration data</param>
        public Task WriteConfigAsync(string serializedConfigData, CancellationToken cancellationToken = default);

        /// <summary>
        /// Saves the provided gearset data to file for the provided character contentId
        /// </summary>
        /// <param name="contentId">The content id of the character to save the gearset data to</param>
        /// <param name="serializedGearsetsList">The serialized gearset data to save</param>
        public Task WriteGearsetsAsync(ulong contentId, string serializedGearsetsList, CancellationToken cancellationToken = default);
    }
}
