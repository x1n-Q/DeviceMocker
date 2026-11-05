using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using DeviceMocker.Core;
using DeviceMocker.Interfaces;
using DeviceMocker.Models;

namespace DeviceMocker.Services
{
    public class SettingsService
    {
        private readonly IStorageService _storage;
        private AppSettings? _cachedSettings;

        public SettingsService(IStorageService storage)
        {
            _storage = storage;
        }

        public async Task<AppSettings> LoadAsync()
        {
            try
            {
                _cachedSettings = await _storage.LoadAsync<AppSettings>(AppConstants.SettingsFile).ConfigureAwait(false);
            }
            catch (JsonException ex)
            {
                var backupPath = $"{AppConstants.SettingsFile}.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}";
                if (File.Exists(AppConstants.SettingsFile))
                    File.Move(AppConstants.SettingsFile, backupPath, true);
                AppLogger.Error($"Invalid settings were moved to '{backupPath}'.", ex);
                _cachedSettings = null;
            }
            if (_cachedSettings == null)
            {
                _cachedSettings = new AppSettings();
                await SaveAsync(_cachedSettings).ConfigureAwait(false);
            }
            return _cachedSettings;
        }

        public async Task SaveAsync(AppSettings settings)
        {
            _cachedSettings = settings;
            await _storage.SaveAsync(AppConstants.SettingsFile, settings).ConfigureAwait(false);
        }

        public AppSettings Current => _cachedSettings ?? new AppSettings();
    }
}
