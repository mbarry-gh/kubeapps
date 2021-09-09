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
    /// Implement System.CommandLine
    /// </summary>
    public sealed partial class Commands
    {
        private static readonly List<string> EnvVarErrors = new ();
        private static readonly DateTime Now = DateTime.UtcNow;

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
                Description = "Automate common Kubernetes GitOps tasks",

                // todo - change this once the CLI API is stable - makes testing MUCH easier
                TreatUnmatchedTokensAsErrors = false,
            };

            Command add = new ("add", "Add bootstrap service");
            Command remove = new ("remove", "Remove bootstrap service");
            remove.AddAlias("rm");

            Command bs = new (Commands.Bootstrap, "Manage bootstrap services");
            bs.AddAlias("bs");
            bs.AddCommand(add);
            bs.AddCommand(remove);

            add.AddCommand(new (Commands.All, "Add all bootstrap service"));
            remove.AddCommand(new (Commands.All, "Remove all bootstrap service"));

            IEnumerable<string> files = Directory.EnumerateFiles(Dirs.KapBootstrapDir, "*.yaml");

            foreach (string f in files)
            {
                add.AddCommand(new (Path.GetFileNameWithoutExtension(f)));
                remove.AddCommand(new (Path.GetFileNameWithoutExtension(f)));
            }

            Command rm = new ("remove", "Remove app from GitOps");
            rm.AddAlias("rm");

            Command appNew = new (Commands.New, "Create a new app");
            appNew.AddCommand(new (Commands.DotNet, "Create a new Dotnet WebAPI app"));

            Command ls = new ("list", "List the apps running in Kubernetes");
            ls.AddAlias("ls");

            root.AddCommand(new ("add", "Add the app to GitOps"));
            root.AddCommand(bs);
            root.AddCommand(ls);
            root.AddCommand(new (Commands.Build, "Build the app"));
            root.AddCommand(new (Commands.Check, "Check the app endpoint (if configured)"));
            root.AddCommand(new (Commands.Init, "Initialize KubeApps"));
            root.AddCommand(new (Commands.Logs, "Get the Kubernetes app logs"));
            root.AddCommand(appNew);
            root.AddCommand(rm);
            root.AddCommand(new (Commands.Sync, "Sync any GitOps changes"));

            // add the options
            root.AddOption(new Option<bool>(new string[] { "--dry-run", "-d" }, "Validates and displays configuration"));

            // validate dependencies
            root.AddValidator(ValidateDependencies);

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
                if (result != null)
                {
                    // todo - add validation?
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

            // always return 0 (success)
            return 0;
        }
    }
}
