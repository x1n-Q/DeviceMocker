using DeviceMocker.Core;
using DeviceMocker.Services;

namespace DeviceMocker.Tests;

public sealed class SettingsServiceTests
{
    [Fact]
    public async Task LoadAsync_BacksUpInvalidJsonAndRestoresDefaults()
    {
        Directory.CreateDirectory(AppConstants.AppDataFolder);
        foreach (var oldBackup in Directory.GetFiles(AppConstants.AppDataFolder, "settings.json.corrupt-*"))
            File.Delete(oldBackup);
        await File.WriteAllTextAsync(AppConstants.SettingsFile, "{ invalid json");
        var service = new SettingsService(new JsonStorageService());

        var settings = await service.LoadAsync();

        Assert.NotNull(settings);
        Assert.Single(Directory.GetFiles(AppConstants.AppDataFolder, "settings.json.corrupt-*"));
        Assert.NotNull(await new JsonStorageService().LoadAsync<DeviceMocker.Models.AppSettings>(AppConstants.SettingsFile));
    }
}
