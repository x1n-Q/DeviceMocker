using DeviceMocker.Services;

namespace DeviceMocker.Tests;

public sealed class JsonStorageServiceTests
{
    [Fact]
    public async Task SaveAsync_ReplacesExistingFileWithoutLeavingTemporaryFiles()
    {
        var folder = CreateTestFolder();
        var path = Path.Combine(folder, "settings.json");
        var storage = new JsonStorageService();

        await storage.SaveAsync(path, new TestValue("first", 1));
        await storage.SaveAsync(path, new TestValue("second", 2));

        var value = await storage.LoadAsync<TestValue>(path);
        Assert.Equal(new TestValue("second", 2), value);
        Assert.Empty(Directory.GetFiles(folder, "*.tmp"));
    }

    private static string CreateTestFolder()
    {
        var root = Environment.GetEnvironmentVariable("DEVICEMOCKER_TEST_ROOT") ?? Path.GetTempPath();
        var folder = Path.Combine(root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    private sealed record TestValue(string Name, int Count);
}
