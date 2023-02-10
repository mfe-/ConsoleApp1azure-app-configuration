using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppRegistrationClassLibrary;

/// <summary>
/// Description of a certificate.
/// </summary>
public class CertificateDescription
{
    public CertificateSource SourceType { get; set; }

    /// <summary>
    /// Certificate distinguished name.
    /// </summary>
    public string? CertificateDistinguishedName { get; set; }

    /// <summary>
    /// Certificate store path, for instance "CurrentUser/My".
    /// </summary>
    /// <remarks>This property should only be used in conjunction with DistinguishedName or Thumbprint.</remarks>
    public string? CertificateStorePath { get; set; }

    /// <summary>
    /// Path on disk to the certificate.
    /// </summary>
    public string? CertificateDiskPath { get; set; }

    /// <summary>
    /// Path on disk to the certificate password.
    /// </summary>
    public string? CertificatePassword { get; set; }
}
