using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace AppRegistrationClassLibrary
{
    public class AppRegistrationAccessTokenService
    {
        private const string CertificateDesicriptionEmptyExceptionMessage = "Either set the value Secret or CertificateDescription. None of them was set!";
        private readonly IOptionsMonitor<AppRegistrationConfiguration> appRegistrationConfiguration;
        private IConfidentialClientApplication? confidentialClientApplication;

        public AppRegistrationAccessTokenService(IOptionsMonitor<AppRegistrationConfiguration> appRegistrationConfiguration)
        {
            this.appRegistrationConfiguration = appRegistrationConfiguration;
            appRegistrationConfiguration.OnChange(ConfigurationChanged);
        }


        private IConfidentialClientApplication BuildConfigdentialClientApplication()
        {
            var confidentialClientApplicationBuilder = ConfidentialClientApplicationBuilder
                .Create(appRegistrationConfiguration.CurrentValue.ClientId)
                .WithTenantId(appRegistrationConfiguration.CurrentValue.TenantId);

            //check if an custome AuthenticationUrl is set
            if (!String.IsNullOrEmpty(appRegistrationConfiguration.CurrentValue.AuthenticationUrl))
            {
                confidentialClientApplicationBuilder.WithAuthority(new Uri(appRegistrationConfiguration.CurrentValue.AuthenticationUrl));
            }
            //switch between ClientSecrect and Certificate
            if (IsClientSecrectInUse())
            {
                confidentialClientApplicationBuilder.WithClientSecret(appRegistrationConfiguration.CurrentValue.Secret);
                confidentialClientApplication = confidentialClientApplicationBuilder.Build();
            }
            else //not tested
            {
                //load certificate depending on the config
                var clientCertificate = LoadCertificate(appRegistrationConfiguration.CurrentValue.CertificateDescription
                    ?? throw new InvalidOperationException(CertificateDesicriptionEmptyExceptionMessage));
                //set certificate
                confidentialClientApplicationBuilder.WithCertificate(clientCertificate);
                confidentialClientApplication = confidentialClientApplicationBuilder.Build();
            }
            return confidentialClientApplication;

        }

        private bool IsClientSecrectInUse()
            => !String.IsNullOrEmpty(appRegistrationConfiguration.CurrentValue.Secret);

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        /// <remarks>
        /// <see cref="https://learn.microsoft.com/en-us/graph/sdks/choose-authentication-providers?tabs=CS#client-credentials-provider"/>
        /// </remarks>
        /// <exception cref="InvalidOperationException"></exception>
        public TokenCredential ClientCertificateCredentialFactory()
        {
            TokenCredential tokenCredential;
            // Multi-tenant apps can use "common",
            // single-tenant apps must use the tenant ID from the Azure portal
            var tenantId = appRegistrationConfiguration.CurrentValue.TenantId;

            // Values from app registration
            var clientId = appRegistrationConfiguration.CurrentValue.ClientId;
            var clientSecret = appRegistrationConfiguration.CurrentValue.Secret;


            var options = new TokenCredentialOptions
            {
                AuthorityHost = AzureAuthorityHosts.AzurePublicCloud
            };

            if (IsClientSecrectInUse())
            {
                // https://learn.microsoft.com/dotnet/api/azure.identity.clientsecretcredential
                tokenCredential = new ClientSecretCredential(
                tenantId, clientId, clientSecret, options);
            }
            else
            {
                var clientCertificate = LoadCertificate(appRegistrationConfiguration.CurrentValue.CertificateDescription
                    ?? throw new InvalidOperationException(CertificateDesicriptionEmptyExceptionMessage));

                tokenCredential = new ClientCertificateCredential(
                    tenantId, clientId, clientCertificate, options);
            }
            return tokenCredential;
        }

        public Task<AuthenticationResult> GetAccessTokenAsync(IEnumerable<string> scopes, CancellationToken ct = default)
        {
            if (confidentialClientApplication == null)
            {
                confidentialClientApplication = BuildConfigdentialClientApplication();
            }
            var acquireTokenForClientParameter = confidentialClientApplication.AcquireTokenForClient(scopes);

            return acquireTokenForClientParameter.ExecuteAsync(ct);
        }
        public Task<AuthenticationResult> GetAccessTokenAsync(string scope, CancellationToken ct = default)
            => GetAccessTokenAsync(new string[] { scope }, ct);


        private X509Certificate2 LoadCertificate(CertificateDescription certificateDescription)
            => certificateDescription.SourceType switch
            {
                CertificateSource.StoreWithDistinguishedName => GetCertificateFromStorage(certificateDescription.CertificateDistinguishedName),
                CertificateSource.Path => GetCertificateFromPath(certificateDescription.CertificateDiskPath),

                _ => throw new NotSupportedException(
                    $"Please use {nameof(CertificateSource.StoreWithDistinguishedName)} or {nameof(CertificateSource.Path)} implementation.")
            };

        private X509Certificate2 GetCertificateFromPath(string? path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(path, "The path of the certificate should be set");

            if (!File.Exists(path))
                throw new FileNotFoundException("Could not find the certificate file", path);

            var certificate = X509Certificate.CreateFromCertFile(path);
            return new X509Certificate2(certificate);
        }
        private X509Certificate2 GetCertificateFromStorage(string? certificateDistinguishedName)
        {
            if (string.IsNullOrEmpty(certificateDistinguishedName))
                throw new ArgumentNullException(certificateDistinguishedName, "The path of the certificate should be provided");

            using var store = new X509Store(StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly);
            var certificate = store.Certificates.Find(X509FindType.FindBySubjectName, certificateDistinguishedName?.Replace("CN=", "") ?? string.Empty, false)
                .Cast<X509Certificate2>().FirstOrDefault();
            return certificate ?? throw new InvalidOperationException($"Could not find the specified certificate ({certificateDistinguishedName}) in the store");
        }

        public AppRegistrationConfiguration AppRegistrationConfiguration
            => appRegistrationConfiguration.CurrentValue;

        private void ConfigurationChanged(AppRegistrationConfiguration appRegistrationConfiguration, string? changed)
        {
            //not fired need to investiage
            //depending on the change confidentialClientApplication needs to be rebuilded 
            //BuildConfigdentialClientApplication();
        }
    }
}
