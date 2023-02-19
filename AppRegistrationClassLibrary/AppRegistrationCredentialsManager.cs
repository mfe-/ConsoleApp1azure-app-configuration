using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Threading;

namespace AppRegistrationClassLibrary
{
    public class AppRegistrationCredentialsManager
    {
        private readonly AppRegistrationAccessTokenService appRegistrationAccessTokenService;
        private readonly IOptionsMonitor<AppRegistrationCredentialsManagerConfiguration> appRegistrationCredentialsManagerConfiguration;
        private readonly ILogger<AppRegistrationCredentialsManager> logger;
        private GraphServiceClient? graphServiceClient;
        private int rotationCounter;

        public AppRegistrationCredentialsManager(AppRegistrationAccessTokenService appRegistrationAccessTokenService, IOptionsMonitor<AppRegistrationCredentialsManagerConfiguration> appRegistrationCredentialsManagerConfiguration, ILogger<AppRegistrationCredentialsManager> logger)
        {
            this.appRegistrationAccessTokenService = appRegistrationAccessTokenService;
            this.appRegistrationCredentialsManagerConfiguration = appRegistrationCredentialsManagerConfiguration;
            this.logger = logger;
        }

        private PeriodicTimer CreateTimer()
        {
            var periodicTimepsan = TimeSpan.ParseExact(appRegistrationCredentialsManagerConfiguration.CurrentValue.RefreshCycle, "c", null);
            return new PeriodicTimer(periodicTimepsan);
        }

        public async Task KeepCredentialsUpToDateAsync(CancellationToken cancellationToken = default)
        {
            var timer = CreateTimer();
            logger.LogTrace("Created PeriodicTimer");


            //check if this service created its own generated credentials and add them if not available (onetime)
            var tenatIdSecretPath = GetTenatIdSecretPath();
            if (!System.IO.File.Exists(tenatIdSecretPath))
            {
                await AddSecretAsync();
            }
            else
            {
                var sucessfull = await ReadSecrectFromFileAndUpdateAccessTokenServiceAsync(tenatIdSecretPath);
                if (!sucessfull)
                {
                    await AddSecretAsync();
                }
            }

            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                //get/update current application
                graphServiceClient = null;
                var app = await GetAppRegistrationAsync(cancellationToken);
                var credentialList = new List<PasswordCredential>();
                foreach (var passwordCredential in app.PasswordCredentials)
                {
                    if (passwordCredential.EndDateTime < DateTimeOffset.Now)
                    {
                        //remove all expired tokens
                        logger.LogWarning("Remove passwordCredential {displayName} {SecretID} as it expired on {enddate}", passwordCredential.DisplayName, passwordCredential.KeyId, passwordCredential.EndDateTime);
                        if (passwordCredential.KeyId != null)
                        {
                            await RemoveSecrectAsync(app, passwordCredential.KeyId.Value);
                        }
                    }
                    else
                    {
                        credentialList.Add(passwordCredential);
                        //collect all valid tokens from our appregistration and select the token with the longest lifetime
                        //check if runs out in x (config from appsettings) time
                        //if yes create new secrect
                    }
                }
                var longestValidToken = credentialList
                    .Where(a => a.DisplayName.StartsWith(GenerateSecretDisplayName()))
                    .Where(a => a.EndDateTime != null)
                    .OrderByDescending(a => (a.EndDateTime!.Value - DateTimeOffset.Now))
                    .First();

                var endsInTimeSpan = (longestValidToken.EndDateTime - DateTimeOffset.Now)!.Value;
                logger.LogTrace("Token lifetime runs out in {days} and {minutes}", endsInTimeSpan.Days, endsInTimeSpan.Minutes);

                var thresholdEndLifeTime = TimeSpan.ParseExact(appRegistrationCredentialsManagerConfiguration.CurrentValue.ThresholdEndLifeTime, "c", null);

                if (endsInTimeSpan < thresholdEndLifeTime)
                {
                    logger.LogInformation("Adding new secrect as the current will expire soon.");
                    //add new secret
                    await AddSecretAsync(app);
                }

                //check for expiring credentials
                // - create new credentials
                // - update publish AppRegistrationAccessTokenService to new credentials
                //remove secondary credentials

            }
        }

        private string GetTenatIdSecretPath()
        {
            if (string.IsNullOrEmpty(appRegistrationAccessTokenService.AppRegistrationConfiguration.TenantId)) throw new InvalidOperationException($"Value for {nameof(appRegistrationAccessTokenService.AppRegistrationConfiguration.TenantId)} is null or empty");

            return Path.Combine(Environment.CurrentDirectory, appRegistrationAccessTokenService.AppRegistrationConfiguration.TenantId);
        }

        public GraphServiceClient BuildGraphServiceClientAsync()
        {
            logger.LogTrace("Building a GraphServiceClient instance");
            var scopes = new[] { "https://graph.microsoft.com/.default" };

            var tokenCredential = appRegistrationAccessTokenService.ClientCertificateCredentialFactory();

            var graphClient = new GraphServiceClient(tokenCredential, scopes);
            return graphClient;
        }

        public GraphServiceClient GraphServiceClient
        {
            get
            {
                if (graphServiceClient is not Microsoft.Graph.GraphServiceClient)
                {
                    graphServiceClient = BuildGraphServiceClientAsync();
                }
                return graphServiceClient;
            }
        }
        public async Task<Application> GetAppRegistrationAsync(CancellationToken cancellationToken = default)
        {
            var applicationCollection = await GraphServiceClient.Applications.Request().GetAsync();
            var applications = new List<Application>();

            var applicationFunc = (Application app) =>
            {
                if (app.AppId == appRegistrationAccessTokenService.AppRegistrationConfiguration.ClientId)
                {
                    applications.Add(app);
                }
                return !cancellationToken.IsCancellationRequested;
            };

            var pageIterator = PageIterator<Application>.CreatePageIterator(graphServiceClient, applicationCollection, applicationFunc);

            await pageIterator.IterateAsync();
            //we expect to have only one application with the ClientId
            return applications.Single();

        }
        /// <summary>
        /// 
        /// </summary>
        /// <remarks>
        /// Requires Permission https://graph.microsoft.com/Application.ReadWrite.OwnedBy
        /// </remarks>
        /// <param name="application"></param>
        /// <returns></returns>
        public async Task AddSecretAsync(Application application)
        {

            rotationCounter++;
            var displayName = $"{GenerateSecretDisplayName()} {rotationCounter}";
            var passwordCred = new PasswordCredential
            {
                DisplayName = displayName,
                StartDateTime = DateTime.Now,
                EndDateTime = GenerateEndTime()
            };

            var appCollection = GraphServiceClient.Applications[application.Id];
            var passwordRequestBuilder = appCollection.AddPassword(passwordCred);
            var graphResponse = await passwordRequestBuilder.Request().PostResponseAsync();

            if (graphResponse.StatusCode == System.Net.HttpStatusCode.OK)
            {
                var passwordCredential = await graphResponse.GetResponseObjectAsync();

                var tenatIdSecretPath = GetTenatIdSecretPath();
                //if (!System.IO.File.Exists(tenatIdSecretPath))
                {
                    await System.IO.File.WriteAllTextAsync(tenatIdSecretPath, passwordCredential.SecretText);
                    logger.LogTrace("worte file with new secrect {SecretText}", passwordCredential.SecretText.ObscureSecret());
                    await ReadSecrectFromFileAndUpdateAccessTokenServiceAsync(tenatIdSecretPath);
                }
            }
        }

        public async Task AddSecretAsync(CancellationToken cancellationToken = default)
        {
            var app = await GetAppRegistrationAsync(cancellationToken);
            await AddSecretAsync(app);
        }

        private async Task<bool> ReadSecrectFromFileAndUpdateAccessTokenServiceAsync(string tenatIdSecretPath)
        {
            var success = false;
            var previousSecrect = appRegistrationAccessTokenService.AppRegistrationConfiguration.Secret;
            var fileSecrect = System.IO.File.ReadAllText(tenatIdSecretPath);
            appRegistrationAccessTokenService.UpdateSecrect(fileSecrect);
            //check if secrect is valid
            using var cancellationTokenSource = new CancellationTokenSource();
            try
            {
                graphServiceClient = null;
                _ = await GetAppRegistrationAsync(cancellationTokenSource.Token);
                success = true;
            }
            catch (ServiceException ex)
            {
                logger.LogError(ex, "Could not retriev the app using the loaded file secret");
                if (!String.IsNullOrEmpty(previousSecrect)) appRegistrationAccessTokenService.UpdateSecrect(previousSecrect);
                //force a refresh of the graphServiceClient
                graphServiceClient = null;
                success = false;
            }
            return success;
        }

        private string GenerateSecretDisplayName()
        {
            var displayName = appRegistrationCredentialsManagerConfiguration.CurrentValue.PasswordCredentialDisplayName;
            return displayName;
        }

        private DateTime GenerateEndTime()
        {
            var credentialsLifeTimeSpan = TimeSpan.ParseExact(appRegistrationCredentialsManagerConfiguration.CurrentValue.CredentialsLifeTimeSpan, "c", null);
            return DateTime.Now.Add(credentialsLifeTimeSpan);
        }

        public async Task RemoveSecrectAsync(Application application, Guid secrectGuid)
        {
            var appCollection = GraphServiceClient.Applications[application.Id];
            var passwordRemoveBuilder = appCollection.RemovePassword(secrectGuid);
            _ = await passwordRemoveBuilder.Request().PostResponseAsync();
        }
        public async Task AddCertificate(Application application)
        {
            var passwordCredential = new PasswordCredential
            {
                DisplayName = "Password friendly name",
                StartDateTime = DateTime.Now,
                EndDateTime = DateTime.Now.AddMinutes(3),
                Hint = "some hints",
            };


            var appCollection = GraphServiceClient.Applications[application.Id];
            var passwordRequestBuilder = appCollection.AddPassword(passwordCredential);

            var ok = await passwordRequestBuilder.Request().PostResponseAsync();

        }

    }
}
