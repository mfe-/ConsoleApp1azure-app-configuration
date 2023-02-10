// See https://aka.ms/new-console-template for more information
using Azure.Core; //nuget Azure.Core
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System.Runtime.ConstrainedExecution;
using Microsoft.Extensions.Hosting;
using System.Reflection;
using CommandLine;
using Microsoft.Extensions.DependencyInjection;
using AppRegistrationClassLibrary;

Console.WriteLine("Hello, World!");

var parseArgumentsOptions = Parser.Default.ParseArguments<ArgumentParserOptions>(args).Value ?? new ArgumentParserOptions();
var appsettingsFile = parseArgumentsOptions.AppSettingsConfig ?? "appsettings.json";

var hostApplicationBuilder = Host.CreateDefaultBuilder(args);
hostApplicationBuilder.ConfigureAppConfiguration((context, configurationBuilder) =>
{
    var location = Assembly.GetExecutingAssembly().Location;
    var basepath = Path.GetDirectoryName(location) ?? throw new InvalidOperationException($"{nameof(Path.GetDirectoryName)} is expected to return a value with parameter GetExecutingAssembly location");
    configurationBuilder.SetBasePath(basepath).AddJsonFile(appsettingsFile, true, true);

});
hostApplicationBuilder.ConfigureServices((hostApplicationBuilder, serviceCollection) =>
{
    serviceCollection.Configure<AppRegistrationConfiguration>(options => hostApplicationBuilder.Configuration.GetSection("AzureAd").Bind(options));

    serviceCollection.AddSingleton<AppRegistrationAccessTokenService>();

    serviceCollection.AddSingleton<AppRegistrationCredentialsManager>();
});


var host = hostApplicationBuilder.Build();


var appRegistrationAccessTokenService = host.Services.GetRequiredService<AppRegistrationAccessTokenService>();


var token = await appRegistrationAccessTokenService.GetAccessTokenAsync("https://graph.microsoft.com/.default");

//await GetApplicationsByGraphApiAsync(token.AccessToken);

var appRegistrationCredentialsManager = host.Services.GetRequiredService<AppRegistrationCredentialsManager>();

var applications = await appRegistrationCredentialsManager.GetAppRegistration();

ShowAppInfo(applications);
await appRegistrationCredentialsManager.AddSecretAsync(applications.First());
ShowAppInfo(applications);

Console.ReadLine();

async Task<AccessToken> GetAccessTokenByScopeAsync(string tenantid, string scope, string clientId, string registeredAppSecrect)
{
    //https://docs.microsoft.com/en-us/graph/auth-v2-service#4-get-an-access-token
    var formUrlEncodedContent = new FormUrlEncodedContent(new[]
        {
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("client_secret", registeredAppSecrect),
                new KeyValuePair<string, string>("scope", scope),
                new KeyValuePair<string, string>("grant_type", "client_credentials")
            });
    HttpClient httpClient = new HttpClient();
    var responseMessage = await httpClient.PostAsync($"https://login.microsoftonline.com/{tenantid}/oauth2/v2.0/token", formUrlEncodedContent);
    var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(await responseMessage.Content.ReadAsStringAsync());
    if (tokenResponse == null) throw new InvalidOperationException($"{nameof(TokenResponse)} expected!");
    return new AccessToken(tokenResponse.access_token, DateTimeOffset.Now + TimeSpan.FromSeconds(tokenResponse.expires_in));
}



async Task GetApplicationsByGraphApiAsync(string accessToken)
{
    //if (DateTimeOffset.Now > accessToken.ExpiresOn)
    {
        //call again GetAccessTokenByScopeAsync
    }
    HttpClient httpClient = new HttpClient();
    var defaultRequetHeaders = httpClient.DefaultRequestHeaders;
    if (defaultRequetHeaders.Accept == null || !defaultRequetHeaders.Accept.Any(m => m.MediaType == "application/json"))
    {
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }
    defaultRequetHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    var responseMessage = await httpClient.GetAsync($"https://graph.microsoft.com/v1.0/applications/");
    var outputString = await responseMessage.Content.ReadAsStringAsync();
    using var jDoc = JsonDocument.Parse(outputString);
    outputString = JsonSerializer.Serialize(jDoc, new JsonSerializerOptions { WriteIndented = true });
    Console.WriteLine(outputString);

    responseMessage = await httpClient.GetAsync($"https://graph.microsoft.com/v1.0/users/");
    outputString = await responseMessage.Content.ReadAsStringAsync();
    Console.WriteLine(outputString);
    //Calling the /me endpoint requires a signed-in user and therefore a delegated permission.
    //Application permissions are not supported when using the /me endpoint.
    //responseMessage = await httpClient.GetAsync($"https://graph.microsoft.com/v1.0/me/");
    //outputString = await responseMessage.Content.ReadAsStringAsync();
    //Console.WriteLine(outputString);
}

static void ShowAppInfo(IEnumerable<Microsoft.Graph.Application> applications)
{
    foreach (var app in applications)
    {
        Console.WriteLine();
        Console.WriteLine(app.DisplayName);
        //Console.WriteLine("KeyCredentials");
        //foreach (var keyCredential in app.KeyCredentials)
        //{
        //    Console.WriteLine(keyCredential.DisplayName);
        //}
        Console.WriteLine("PasswordCredentials:");
        foreach (var passwordCredential in app.PasswordCredentials)
        {
            Console.WriteLine($"{passwordCredential.DisplayName} {passwordCredential.StartDateTime}-{passwordCredential.EndDateTime}");
        }
    }
}
/// <summary>
/// https://docs.microsoft.com/en-us/graph/auth-v2-service#token-response
/// </summary>
public class TokenResponse
{
    public string token_type { get; set; }
    public int expires_in { get; set; }
    public int ext_expires_in { get; set; }
    public string access_token { get; set; }
}