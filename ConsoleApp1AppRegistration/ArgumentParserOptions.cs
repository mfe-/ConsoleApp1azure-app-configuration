// See https://aka.ms/new-console-template for more information
using CommandLine.Text;
using CommandLine;

internal class ArgumentParserOptions
{
    /// <summary>
    /// Specify the appsettings.json file to use
    /// </summary>
    /// /// <remarks>
    /// default file used is 'appsettings.json'
    /// example: --appSettingsConfig appsettings.abn.json
    /// </remarks>
    [Option('c', "appSettingsConfig", Required = false, HelpText = "Specify which appsettings.json file to use.")]
    public string? AppSettingsConfig { get; set; }
}