namespace GraphDbSkillAutomation;

public static class FileHelper
{
    private static readonly HttpClient Client = new();

    public static void DownloadFile(string destination, string url)
    {
        var response = Client.GetAsync(url).Result;
        response.EnsureSuccessStatusCode();
            
        using var fileStream = File.Create(destination);
        response.Content.CopyToAsync(fileStream).Wait();
    }

    public static string GetAbsolutePath(string path)
    {
        if (path.StartsWith('~'))
        {
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(homeDir, path.TrimStart('~', '/'));
        }
        
        return Path.GetFullPath(path);
    }
}