using System.Reflection;

namespace AppRegistrationClassLibrary;

public class AppRegistrationCredentialsManagerConfiguration
{
    /// <summary>
    /// Get or sets how often to get and update the credentials.
    /// </summary>
    /// <remarks>
    /// TimeSpan serialized as string with <code>new System.TimeSpan(0,0,3,0).ToString("c")</code> Sample value <code>00:03:00</code>
    /// </remarks>
    public string RefreshCycle { get; set; } = "00:05:00";
    /// <summary>
    /// This value will be used for <seealso cref="Microsoft.Graph.PasswordCredential.EndDateTime"/> when generating the secret.
    /// The Endtime will be calculated like the following <seealso cref="DateTime.Now"/> + <seealso cref="CredentialsLifeTimeSpan"/>
    /// </summary>
    public string CredentialsLifeTimeSpan { get; set; } = "00:05:00";
    /// <summary>
    /// Gets or sets the Displayname which should be used for the generated tokens
    /// </summary>
    public string PasswordCredentialDisplayName { get; set; } = $"By {Assembly.GetEntryAssembly()?.GetName().Name} ";
    public string ThresholdEndLifeTime { get; set; } = "00:05:00";

}