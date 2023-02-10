﻿using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace AppRegistrationClassLibrary
{
    public class AppRegistrationCredentialsManager
    {
        private readonly AppRegistrationAccessTokenService appRegistrationAccessTokenService;
        private GraphServiceClient? graphServiceClient;

        public AppRegistrationCredentialsManager(AppRegistrationAccessTokenService appRegistrationAccessTokenService)
        {
            this.appRegistrationAccessTokenService = appRegistrationAccessTokenService;
        }
        public GraphServiceClient BuildGraphServiceClientAsync()
        {
            var scopes = new[] { "https://graph.microsoft.com/.default" };

            var tokenCredential = appRegistrationAccessTokenService.ClientCertificateCredentialFactory();

            var graphClient = new GraphServiceClient(tokenCredential, scopes);
            return graphClient;
        }
        public async Task<IEnumerable<Application>> GetAppRegistration(CancellationToken cancellationToken = default)
        {
            if (graphServiceClient is not GraphServiceClient)
            {
                graphServiceClient = BuildGraphServiceClientAsync();
            }

            var applicationCollection = await graphServiceClient.Applications.Request().GetAsync();
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
            return applications;

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
            if (graphServiceClient is not GraphServiceClient)
            {
                graphServiceClient = BuildGraphServiceClientAsync();
            }
            var passwordCredential = new PasswordCredential
            {
                DisplayName = $"Auto. generated by {Assembly.GetExecutingAssembly()}",
                StartDateTime = DateTime.Now,
                EndDateTime = DateTime.Now.AddMinutes(3),
                Hint = "some hints",
            };


            var appCollection = graphServiceClient.Applications[application.Id];
            var passwordRequestBuilder = appCollection.AddPassword(passwordCredential);

            var ok = await passwordRequestBuilder.Request().PostAsync();
            
        }

        public async Task AddCertificate(Application application)
        {
            if (graphServiceClient is not GraphServiceClient)
            {
                graphServiceClient = BuildGraphServiceClientAsync();
            }
            var passwordCredential = new PasswordCredential
            {
                DisplayName = "Password friendly name",
                StartDateTime = DateTime.Now,
                EndDateTime = DateTime.Now.AddMinutes(3),
                Hint = "some hints",
            };


            var appCollection = graphServiceClient.Applications[application.Id];
            var passwordRequestBuilder = appCollection.AddPassword(passwordCredential);

            var ok = await passwordRequestBuilder.Request().PostResponseAsync();

        }

    }
}