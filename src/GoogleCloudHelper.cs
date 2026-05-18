namespace GraphDbSkillAutomation;

public class GoogleCloudHelper
{
    public static void AssertValidADC()
    {
        Console.WriteLine("Asserting valid Google Cloud Application Default Credentials...");
        
        var gcloudAdc = FileHelper.GetAbsolutePath("~/.config/gcloud/application_default_credentials.json");
        if (!File.Exists(gcloudAdc))
        {
            throw new Exception("Google Cloud Application Default Credentials is not setup. Please run 'gcloud auth application-default login'.");
        }

        if (!ShellHelper.IsCommandAvailable("gcloud"))
        {
            throw new Exception("gcloud was not found in your PATH.");
        }

        var accessTokenResult = ShellHelper.RunCommand(new CommandParams
        {
            Command = "gcloud",
            Args = "auth application-default print-access-token",
            OutputToConsole = false
        });
        if (!accessTokenResult.Success)
        {
            throw new Exception(
                "Google Cloud Application Default Credentials need to be refreshed. Please run 'gcloud auth application-default login");
        }
    }
}