// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;

namespace Kube.Apps
{
    /// <summary>
    /// Main application class
    /// </summary>
    public sealed partial class App
    {
        private static readonly List<string> EnvVarErrors = new ();
        private static readonly DateTime Now = DateTime.UtcNow;

        // private static Dictionary<string, object> appConfig = null;
        // private static List<string> targets = null;

        public static Config Config { get; set; } = null;

        /// <summary>
        /// Run the app
        /// </summary>
        /// <param name="config">command line config</param>
        /// <returns>status</returns>
        public static int RunApp(Config config)
        {
            Config = config;

            return 1;
        }

        /// <summary>
        /// Build the RootCommand for parsing
        /// </summary>
        /// <returns>RootCommand</returns>
        public static RootCommand BuildRootCommand()
        {
            RootCommand root = new ()
            {
                Name = "KubeApps",
                Description = "Generate GitOps files for Flux",
                TreatUnmatchedTokensAsErrors = false,
            };

            Command add = new ("add", "Add bootstrap components");
            Command remove = new ("remove", "Remove bootstrap components");
            Command deploy = new ("deploy", "Deploy any GitOps changes");
            Command app = new ("app", "Application sub-commands");

            add.AddCommand(new ("all", "Add all bootstrap components"));
            remove.AddCommand(new ("all", "Remove all bootstrap components"));

            IEnumerable<string> files = Directory.EnumerateFiles(Dirs.KapBootstrapDir, "*.yaml");

            if (files.Any())
            {
                foreach (string f in files)
                {
                    add.AddCommand(new (Path.GetFileNameWithoutExtension(f)));
                    remove.AddCommand(new (Path.GetFileNameWithoutExtension(f)));
                }
            }

            app.AddCommand(new ("build", "Build the app"));
            app.AddCommand(new ("check", "Check the app endpoint (if configured)"));
            app.AddCommand(new ("deploy", "Deploy the app"));
            app.AddCommand(new ("init", "Initialize KubeApps"));
            app.AddCommand(new ("logs", "Get the Kubernetes app logs"));
            app.AddCommand(new ("remove", "Remove the app via GitOps"));

            Command appNew = new ("new", "Create a new application");
            appNew.AddCommand(new ("dotnet", "Create a new Dotnet WebAPI app"));
            app.AddCommand(appNew);

            root.AddCommand(add);
            root.AddCommand(app);
            root.AddCommand(deploy);
            root.AddCommand(remove);

            // add the options
            //root.AddOption(new Option<bool>(new string[] { "--dry-run", "-d" }, "Validates and displays configuration"));

            //// validate dependencies
            //root.AddValidator(ValidateDependencies);

            return root;
        }

        // validate combinations of parameters
        private static string ValidateDependencies(CommandResult result)
        {
            string msg = string.Empty;

            if (EnvVarErrors.Count > 0)
            {
                msg += string.Join('\n', EnvVarErrors) + '\n';
            }

            try
            {
                // get the values to validate
                string user = result.Children.FirstOrDefault(c => c.Symbol.Name == "ago-user") is OptionResult userRes ? userRes.GetValueOrDefault<string>() : string.Empty;
                string pat = result.Children.FirstOrDefault(c => c.Symbol.Name == "ago-pat") is OptionResult patRes ? patRes.GetValueOrDefault<string>() : string.Empty;
                string repo = result.Children.FirstOrDefault(c => c.Symbol.Name == "ago-repo") is OptionResult repoRes ? repoRes.GetValueOrDefault<string>() : string.Empty;
                string template = result.Children.FirstOrDefault(c => c.Symbol.Name == "template-dir") is OptionResult templateRes ? templateRes.GetValueOrDefault<string>() : string.Empty;
                string output = result.Children.FirstOrDefault(c => c.Symbol.Name == "output-dir") is OptionResult outputRes ? outputRes.GetValueOrDefault<string>() : string.Empty;
                bool localdev = result.Children.FirstOrDefault(c => c.Symbol.Name == "local-dev") is OptionResult localDevRes && localDevRes.GetValueOrDefault<bool>();

                // validate data-service
                if (localdev)
                {
                    if (string.IsNullOrWhiteSpace(template))
                    {
                        msg += "--template-dir cannot be empty\n";
                    }

                    if (string.IsNullOrWhiteSpace(output))
                    {
                        msg += "--output-dir cannot be empty\n";
                    }
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(user))
                    {
                        msg += "--ago-user cannot be empty\n";
                    }

                    if (string.IsNullOrWhiteSpace(pat))
                    {
                        msg += "--ago-pat cannot be empty\n";
                    }

                    if (string.IsNullOrWhiteSpace(repo))
                    {
                        msg += "--ago-repo cannot be empty\n";
                    }
                }
            }
            catch
            {
                // system.commandline will catch and display parse exceptions
            }

            // return error message(s) or string.empty
            return msg;
        }

        // insert env vars as default
        private static Option EnvVarOption<T>(string[] names, string description, T defaultValue)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                throw new ArgumentNullException(nameof(description));
            }

            // this will throw on bad names
            string env = GetValueFromEnvironment(names, out string key);

            T value = defaultValue;

            // set default to environment value if set
            if (!string.IsNullOrWhiteSpace(env))
            {
                if (defaultValue.GetType().IsEnum)
                {
                    if (Enum.TryParse(defaultValue.GetType(), env, true, out object result))
                    {
                        value = (T)result;
                    }
                    else
                    {
                        EnvVarErrors.Add($"Environment variable {key} is invalid");
                    }
                }
                else
                {
                    try
                    {
                        value = (T)Convert.ChangeType(env, typeof(T));
                    }
                    catch
                    {
                        EnvVarErrors.Add($"Environment variable {key} is invalid");
                    }
                }
            }

            return new Option<T>(names, () => value, description);
        }

        // insert env vars as default with min val for ints
        private static Option<int> EnvVarOption(string[] names, string description, int defaultValue, int minValue)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                throw new ArgumentNullException(nameof(description));
            }

            // this will throw on bad names
            string env = GetValueFromEnvironment(names, out string key);

            int value = defaultValue;

            // set default to environment value if set
            if (!string.IsNullOrWhiteSpace(env))
            {
                if (!int.TryParse(env, out value))
                {
                    EnvVarErrors.Add($"Environment variable {key} is invalid");
                }
            }

            Option<int> opt = new (names, () => value, description);

            opt.AddValidator((res) =>
            {
                string s = string.Empty;
                int val;

                try
                {
                    val = (int)res.GetValueOrDefault();

                    if (val <= minValue)
                    {
                        s = $"{names[0]} must be >= {minValue}";
                    }
                }
                catch
                {
                }

                return s;
            });

            return opt;
        }

        // check for environment variable value
        private static string GetValueFromEnvironment(string[] names, out string key)
        {
            if (names == null ||
                names.Length < 1 ||
                names[0].Trim().Length < 4)
            {
                throw new ArgumentNullException(nameof(names));
            }

            for (int i = 1; i < names.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(names[i]) ||
                    names[i].Length != 2 ||
                    names[i][0] != '-')
                {
                    throw new ArgumentException($"Invalid command line parameter at position {i}", nameof(names));
                }
            }

            key = names[0][2..].Trim().ToUpperInvariant().Replace('-', '_');

            return Environment.GetEnvironmentVariable(key);
        }

        // Display the dry run message
        private static int DoDryRun()
        {
            Console.WriteLine($"Version              {VersionExtension.Version}");

            if (!string.IsNullOrWhiteSpace(Config.ContainerVersion))
            {
                Console.WriteLine($"Container Version    {Config.ContainerVersion}");
            }

            if (Config.LocalDev)
            {
                Console.WriteLine($"Template Directory   {Config.TemplateDir}");
                Console.WriteLine($"Output Directory     {Config.OutputDir}");
            }
            else
            {
                Console.WriteLine($"GitHub User          {Config.AgoUser}");
                Console.WriteLine($"GitHub PAT           Length: {(string.IsNullOrWhiteSpace(Config.AgoPat) ? 0 : Config.AgoPat.Length)}");
                Console.WriteLine($"GitHub Repo          {Config.AgoRepo}");
            }

            // always return 0 (success)
            return 0;
        }
    }
}
