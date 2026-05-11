namespace GraphDbSkillAutomation;

public static class FileHelper
{
    public static void DownloadFile(string destination, string url)
    {
        using var client = new HttpClient();
        var response = client.GetAsync(url).Result;
        response.EnsureSuccessStatusCode();
            
        using var fileStream = File.Create(destination);
        response.Content.CopyToAsync(fileStream).Wait();
    }
}