using PureCloudPlatform.Client.V2.Client;
using PureCloudPlatform.Client.V2.Extensions;
using Microsoft.Extensions.Configuration;
using System.Net;
using Tommy;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.ComponentModel;
using System.Globalization;
using PureCloudPlatform.Client.V2.Model;
using System;

namespace CallFlowVisualizer
{
    internal class FetchGCAccessToken
    {
        /// <summary>
        /// Get GenesysCloud Access Token
        /// </summary>
        internal static void GetAccessToken(string profile)
        {
            var configRoot = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile(path: "appsettings.json").Build(); ;

            string gcProfileFileName = configRoot.GetSection("gcSettings").Get<GcSettings>().GcProfileFileName; ;
            string gcEndpoint = configRoot.GetSection("gcSettings").Get<GcSettings>().Endpoint;


			if (!Path.IsPathRooted(gcProfileFileName))
            {
                gcProfileFileName= Path.Combine(Directory.GetCurrentDirectory(), gcProfileFileName);

            }

            if (!File.Exists(gcProfileFileName))
            {
                ColorConsole.WriteError($"{gcProfileFileName} does not exists. Create config.toml file.");
                Environment.Exit(1);

            }


            string environment;
            string client_credentials;
            string client_secret;
            string proxyUserName;
            string proxyPassword;
            TomlTable table = new();

            using (StreamReader reader = File.OpenText(gcProfileFileName))
            {
                try
                {
                    table = TOML.Parse(reader);

                }
                catch (Exception)
                {
                    ColorConsole.WriteError($"{gcProfileFileName} toml file format error. Modify .toml file properly.");
                    Environment.Exit(1);

                }
                
                environment = table[profile]["environment"];
                client_credentials = table[profile]["client_credentials"];
                client_secret = table[profile]["client_secret"];
                proxyUserName = table[profile]["proxy_username"];
                proxyPassword = table[profile]["proxy_password"];

            }

            if (proxyUserName == "Tommy.TomlLazy" || proxyPassword == "Tommy.TomlLazy")
            {
                proxyUserName = null;
                proxyPassword = null;

            }


            string gcRegion = GetRegionHost(environment);

			int maxRetryTimeSec = configRoot.GetSection("gcSettings").Get<GcSettings>().MaxRetryTimeSec; ;
            int retryMax = configRoot.GetSection("gcSettings").Get<GcSettings>().RetryMax;

            bool useProxy = configRoot.GetSection("proxySettings").Get<ProxySettings>().UseProxy;
            string proxyServerAddress = configRoot.GetSection("proxySettings").Get<ProxySettings>().ProxyServerAddress;
            bool useProxyAuth = !string.IsNullOrEmpty(proxyUserName);


			//[MOD] 2024/10/28

			//if (string.IsNullOrEmpty(gcRegion) || gcRegion== "Tommy.TomlLazy" || client_credentials== "Tommy.TomlLazy" || client_secret=="Tommy.TomlLazy")
			//{
			//    ColorConsole.WriteError(@"Required parameter for GenesysCloud API was not set. Set environment and client credentials in .toml file.");
			//    Environment.Exit(1);

			//}

			if (gcRegion == "Tommy.TomlLazy" || client_credentials == "Tommy.TomlLazy" || client_secret == "Tommy.TomlLazy")
			{
				ColorConsole.WriteError(@"Required parameter for GenesysCloud API was not set. Set environment and client credentials in .toml file.");
				Environment.Exit(1);

			}

			ColorConsole.WriteLine($"Start GetAccessToken.", ConsoleColor.Yellow);

			//[ADD] 2024/10/28
			if (!String.IsNullOrEmpty(gcRegion))
            {

				try
				{
					// Need to change Rest sharp version to 106.3.1 in app.config to resolve error
					PureCloudRegionHosts region = (PureCloudRegionHosts)Enum.Parse(typeof(PureCloudRegionHosts), gcRegion);
					Configuration.Default.ApiClient.setBasePath(region);
				}
				catch (Exception)
				{
					ColorConsole.WriteError(@"Required parameter for GenesysCloud API was not set. Set environment properly in .toml file. e.g. ap_northeast_1 or mypurecloud.jp");
					Environment.Exit(1);
				}

            }
            else
            {
                // For new open regison not listed in Purecloud region hosts
				Configuration.Default.ApiClient.setBasePath(gcEndpoint);

			}


            if (useProxy)
            {
                if (string.IsNullOrEmpty(proxyServerAddress))
                {
                    ColorConsole.WriteError("Proxy Server Address was not set. Set proxyServer Address in appsettings.json");
                    Environment.Exit(1);

                }

                try
                {
                    Configuration.Default.ApiClient.ClientOptions.Proxy = new WebProxy(proxyServerAddress);

                }
                catch (Exception e)
                {
                    ColorConsole.WriteError("Proxy Server Address was not set properly. Set proxyServer Address in appsettings.json " +e.Message);
                    Environment.Exit(1);

                }
                
                if (useProxyAuth)
                {
                    Configuration.Default.ApiClient.ClientOptions.Proxy.Credentials = new NetworkCredential(proxyUserName, proxyPassword);

                }

            }

            try
            {
                var accessTokenInfo = Configuration.Default.ApiClient.PostToken(client_credentials, client_secret);

            }
            catch (Exception e)
            {
                ColorConsole.WriteError("Exception when calling PostToken: " + e.Message);
                Environment.Exit(1);

            }

            var retryConfig = new ApiClient.RetryConfiguration { MaxRetryTimeSec = maxRetryTimeSec, RetryMax = retryMax };
            Configuration.Default.ApiClient.RetryConfig = retryConfig;

            ColorConsole.WriteLine("GetAccessToken done.", ConsoleColor.Yellow);
        }

        /// <summary>
        /// Get region from PureCloudRegionHosts Enum
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private static string GetEnumDescription(PureCloudRegionHosts value)
        {
            FieldInfo fi = value.GetType().GetField(value.ToString());
            var attributes = (DescriptionAttribute[])fi.GetCustomAttributes(typeof(DescriptionAttribute), false);
            var desciptionString = attributes.Select(n => n.Description).FirstOrDefault();

            if (desciptionString != null)
            {
                return desciptionString;

            }

            return value.ToString();
        }

        /// <summary>
        /// Get PureCloudRegionHosts from region or url
        /// </summary>
        /// <param name="gcRegiton"></param>
        /// <returns></returns>
        private static string GetRegionHost(string gcRegiton)
        {
            foreach (var region_i in Enum.GetValues(typeof(PureCloudRegionHosts)))
            {

                if (gcRegiton == region_i.ToString())
                {
                    return region_i.ToString();

                }

                string uri = GetEnumDescription((PureCloudRegionHosts)Enum.Parse(typeof(PureCloudRegionHosts), region_i.ToString()));
                if (uri.Contains(gcRegiton))
                {
                    return region_i.ToString();

                }

            }

            return null;

        }


    }
}
