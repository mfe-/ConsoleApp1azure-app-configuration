// See https://aka.ms/new-console-template for more information
using Azure.Core; //nuget Azure.Core
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

Console.WriteLine("Hello, World!");

var configurationBuilder = new ConfigurationBuilder();
configurationBuilder.AddJsonFile("appsettings.json");

var configurationRoot= configurationBuilder.Build();
var clientid = configurationRoot.GetSection("AzureAd").GetValue<string>("ClientId");
var clientSecret = configurationRoot.GetValue<string>("AzureAd:Secret");
var tenantid = configurationRoot.GetValue<string>("AzureAd:TenantId");


var accessToken =await GetAccessTokenByScopeAsync(tenantid, "https://graph.microsoft.com/.default", clientid, clientSecret);
await GetApplicationsByGraphApiAsync(accessToken);

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
async Task GetApplicationsByGraphApiAsync(AccessToken accessToken)
{
    if (DateTimeOffset.Now > accessToken.ExpiresOn)
    {
        //call again GetAccessTokenByScopeAsync
    }
    HttpClient httpClient = new HttpClient();
    var defaultRequetHeaders = httpClient.DefaultRequestHeaders;
    if (defaultRequetHeaders.Accept == null || !defaultRequetHeaders.Accept.Any(m => m.MediaType == "application/json"))
    {
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }
    defaultRequetHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Token);
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