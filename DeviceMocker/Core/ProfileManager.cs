using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DeviceMocker.Interfaces;
using DeviceMocker.Models;

namespace DeviceMocker.Core
{
    public class ProfileManager : IProfileManager
    {
        private readonly IStorageService _storage;
        private readonly string _profilesFolder;

        public ProfileManager(IStorageService storage, string profilesFolder)
        {
            _storage = storage;
            _profilesFolder = profilesFolder;
            Directory.CreateDirectory(_profilesFolder);
        }

        public async Task<List<DeviceProfile>> GetAllProfilesAsync()
        {
            var profiles = new List<DeviceProfile>();
            if (!Directory.Exists(_profilesFolder)) return profiles;

            foreach (var file in Directory.GetFiles(_profilesFolder, "*.json"))
            {
                try
                {
                    var profile = await _storage.LoadAsync<DeviceProfile>(file);
                    if (profile != null)
                        profiles.Add(profile);
                }
                catch (Exception ex)
                {
                    Services.AppLogger.Error($"Unable to load profile '{file}'.", ex);
                }
            }
            return profiles.OrderByDescending(p => p.UpdatedAt).ToList();
        }

        public async Task<DeviceProfile?> GetProfileAsync(string id)
        {
            var path = GetProfilePath(id);
            return await _storage.LoadAsync<DeviceProfile>(path);
        }

        public async Task SaveProfileAsync(DeviceProfile profile)
        {
            profile.UpdatedAt = DateTime.Now;
            var path = GetProfilePath(profile.Id);
            await _storage.SaveAsync(path, profile);
        }

        public async Task DeleteProfileAsync(string id)
        {
            var path = GetProfilePath(id);
            if (File.Exists(path))
            {
                File.Delete(path);
                await Task.CompletedTask;
            }
        }

        public async Task<DeviceProfile> DuplicateProfileAsync(string id)
        {
            var original = await GetProfileAsync(id);
            if (original == null)
                throw new InvalidOperationException($"Profile '{id}' not found.");

            var duplicate = new DeviceProfile
            {
                Id = Guid.NewGuid().ToString(),
                Name = $"{original.Name} (Copy)",
                DeviceType = original.DeviceType,
                Description = original.Description,
                DefaultOutputChannel = original.DefaultOutputChannel,
                DefaultPrefix = original.DefaultPrefix,
                DefaultSuffix = original.DefaultSuffix,
                DelayPerCharacterMs = original.DelayPerCharacterMs,
                Buttons = original.Buttons.Select(b => new PosButton
                {
                    Id = b.Id,
                    Label = b.Label,
                    ActionType = b.ActionType,
                    Value = b.Value,
                    Prefix = b.Prefix,
                    Suffix = b.Suffix,
                    DelayMs = b.DelayMs
                }).ToList(),
                Settings = new Dictionary<string, string>(original.Settings),
                EmulatorSettings = original.EmulatorSettings?.Clone() ?? new EmulatorProfileSettings(),
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            await SaveProfileAsync(duplicate);
            return duplicate;
        }

        public async Task ExportProfileAsync(string id, string exportPath)
        {
            var profile = await GetProfileAsync(id);
            if (profile == null)
                throw new InvalidOperationException($"Profile '{id}' not found.");
            await _storage.SaveAsync(exportPath, profile);
        }

        public async Task<DeviceProfile> ImportProfileAsync(string importPath)
        {
            var profile = await _storage.LoadAsync<DeviceProfile>(importPath);
            if (profile == null)
                throw new InvalidOperationException("Invalid profile file.");

            profile.Id = Guid.NewGuid().ToString();
            profile.UpdatedAt = DateTime.Now;
            await SaveProfileAsync(profile);
            return profile;
        }

        private string GetProfilePath(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("A profile ID is required.", nameof(id));

            var safeName = string.Join("_", id.Split(Path.GetInvalidFileNameChars()));
            var path = Path.GetFullPath(Path.Combine(_profilesFolder, $"{safeName}.json"));
            var folder = Path.GetFullPath(_profilesFolder) + Path.DirectorySeparatorChar;
            if (!path.StartsWith(folder, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The profile ID resolves outside the profiles folder.");
            return path;
        }
    }
}
