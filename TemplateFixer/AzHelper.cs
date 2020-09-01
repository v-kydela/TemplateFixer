using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace TemplateFixer
{
    internal static class AzHelper
    {
        private const string ResourceName = "templatetest";
        private const string RgPrefix = ResourceName + "rg";
        private const string AppRegPrefix = ResourceName + "appreg";
        private const string Password = "AtLeastSixteenCharacters_0";
        private const string Location = "westus";

        private static string AzCliPath { get; set; }

        internal static bool CheckLoginFailure() => Run("account list-locations").ExitCode != 0 && Run("login").ExitCode != 0;

        internal static IEnumerable<string> ListResourceGroups() =>
            JArray.Parse(Run("group list").Output)
                .Select(group => group[Properties.Name].ToString())
                .Where(name => name.StartsWith(RgPrefix));

        internal static ProcessResult DeleteResourceGroup(string name) => Run($"group delete -n {name}");

        internal static IEnumerable<string> ListAppRegistrations() =>
            JArray.Parse(Run($"ad app list --filter \"startswith(displayName, '{ResourceName}')\"").Output)
                .Select(app => app[Properties.AppId].ToString());

        internal static ProcessResult CreateAppRegistration(long index) =>
            Run($"ad app create --display-name \"{AppRegPrefix}{index}\" --password \"{Password}\" --available-to-other-tenants");

        internal static ProcessResult DeleteAppRegistration(string id) => Run($"ad app delete --id {id}");

        internal static ProcessResult Deploy(DistinctTemplate template, string appId, long index) =>
            Run($"deployment sub create"
                + $" --template-file \"{template.Paths.First()}\""
                + $" --location {Location}"
                + $" --parameters"
                    + $" appId=\"{appId}\""
                    + $" appSecret=\"{Password}\""
                    + $" botId=\"{ResourceName}bot{index}\""
                    + $" botSku=F0"
                    + $" newAppServicePlanName=\"{ResourceName}asp{index}\""
                    + $" newWebAppName=\"{ResourceName}webapp{index}\""
                    + $" groupName=\"{RgPrefix}{index}\""
                    + $" groupLocation=\"{Location}\""
                    + $" newAppServicePlanLocation=\"{Location}\""
                + $" --name \"{ResourceName}deployment{index}\"");

        private static string GetAzCliPath()
        {
            if (string.IsNullOrWhiteSpace(AzCliPath))
            {
                var result = Run("where", "az");

                if (result.ExitCode == 0)
                {
                    AzCliPath = result.Output;
                }
                else
                {
                    throw new Exception("Please install AZ CLI and make sure it's included in your PATH variable.");
                }
            }

            return AzCliPath;
        }

        private static ProcessResult Run(string arguments) => Run(GetAzCliPath(), arguments);

        private static ProcessResult Run(string fileName, string arguments)
        {
            using var process = Process.Start(new ProcessStartInfo(fileName, arguments)
            {
                RedirectStandardOutput = true,
            });

            var output = process.StandardOutput.ReadToEnd().Trim();

            process.WaitForExit();

            return new ProcessResult(
                output,
                process.ExitCode);
        }
    }
}