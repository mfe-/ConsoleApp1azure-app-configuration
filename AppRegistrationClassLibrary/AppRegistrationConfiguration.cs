namespace AppRegistrationClassLibrary;

public class AppRegistrationConfiguration
{
    /// <summary>
    /// Get or sets the name of the Appregistration.
    /// </summary>
    public string? AppRegistrationName { get; set; }
    /// <summary>
    /// Retriev the value from the Azure Portal on the AppRegistration on menue overview "Application (client) ID". 
    /// </summary>
    public string? ClientId { get; set; }
    /// <summary>
    /// Retriev the value from the Azure Portal on the AppRegistration on menue overview "Directory (tenant) ID". 
    /// </summary>x
    public string? TenantId { get; set; }
    /// <summary>
    /// Url to authenticate to a Tenant, e.g.: "https://login.microsoftonline.com/52f884f5-dd61-4b1e-9f6a-81eb9ca2c89a/"
    /// </summary>
    public string? AuthenticationUrl { get; set; }
    /// <summary>
    /// Get or sets the value of the ClientSecret which will be used for the AppRegistration.
    /// </summary>
    public string? Secret { get; set; }
    /// <summary>
    /// Get or sets the value of the CertificateDescription which will be used for the AppRegistration.
    /// </summary>
    public CertificateDescription? CertificateDescription { get; set; }
}
