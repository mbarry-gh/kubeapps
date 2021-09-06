// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace AutoGitOps
{
    /// <summary>
    /// Main application class
    /// </summary>
    public sealed partial class App
    {
        private const string GitOpsDir = "/workspaces/gitops/gitops";
        private const string TemplateFile = "autogitops/template.yaml";

        /// <summary>
        /// Main entry point
        /// </summary>
        /// <param name="args">Command Line Parameters</param>
        /// <returns>0 on success</returns>
        public static int Main(string[] args)
        {
            DisplayAsciiArt(args);

            // build the System.CommandLine.RootCommand
            RootCommand root = BuildRootCommand();

            ParseResult res = root.Parse(args);

            // handle the commands
            if (res.Errors.Count == 0 && res.RootCommandResult.Children.Count > 0)
            {
                switch (res.RootCommandResult.Children[0].Symbol.Name)
                {
                    case "app":
                        return DoApp(res.CommandResult.Command.Name);
                    case "add":
                        return DoAdd(res.CommandResult.Command.Name);
                    case "deploy":
                        return DoDeploy();
                    case "init":
                        return DoInit();
                    default:
                        break;
                }
            }

            // run the app
            root.Handler = CommandHandler.Create<Config>(RunApp);
            return root.Invoke(args);
        }

        private static Dictionary<string, object> ReadAgoConfig()
        {
            DateTime now = DateTime.UtcNow;
            string cfgFile = "autogitops/autogitops.json";

            Dictionary<string, object> cfg = new ();

            if (File.Exists(cfgFile))
            {
                cfg = JsonSerializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(cfgFile), JsonOptions);
            }
            else
            {
                IEnumerable<string> files = Directory.EnumerateFiles(".", "*.csproj");

                if (files.Any())
                {
                    // TODO - read csproj file for assembly name

                    cfg["name"] = Path.GetFileNameWithoutExtension(files.First<string>());
                }
                else
                {
                    cfg["name"] = Path.GetFileName(Directory.GetCurrentDirectory());
                    cfg["image"] = $"k3d-registry.localhost:5000/{cfg["name"]}";
                }
            }

            if (!cfg.ContainsKey("name"))
            {
                cfg["name"] = "app";
            }

            if (!cfg.ContainsKey("imageName"))
            {
                cfg["imageName"] = $"k3d-registry.localhost:5000/{cfg["name"]}";
            }

            if (!cfg.ContainsKey("imageTag"))
            {
                cfg["imageTag"] = "local";
            }

            if (!cfg.ContainsKey("namespace"))
            {
                cfg["namespace"] = "default";
            }

            if (!cfg.ContainsKey("port"))
            {
                cfg["port"] = 8080;
            }

            if (!cfg.ContainsKey("nodePort"))
            {
                cfg["nodePort"] = 30080;
            }

            if (!cfg.ContainsKey("version"))
            {
                cfg["version"] = now.ToString("MMdd-HHmm");
            }

            if (!cfg.ContainsKey("deploy"))
            {
                cfg["deploy"] = now.ToString("yyyy-MM-dd-HH-mm-ss");
            }

            return cfg;
        }

        // handle ago app commands
        private static int DoApp(string cmd)
        {
            Dictionary<string, object> cfg = ReadAgoConfig();

            if (cmd == "init")
            {
                IEnumerable<string> files = Directory.EnumerateFiles(".", "*.csproj");

                if (!files.Any())
                {
                    Console.WriteLine("Could not find .csproj file");
                    return 1;
                }
            }

            switch (cmd)
            {
                case "remove":
                    // delete the file from GitOps
                    string file = Path.Combine(GitOpsDir, $"{cfg["name"]}.yaml");

                    if (File.Exists(file))
                    {
                        File.Delete(file);
                        DoDeploy();
                    }

                    break;

                case "dotnet":
                case "init":
                    // create new dotnet app
                    if (cmd == "dotnet")
                    {
                        DotNetNew();

                        cfg["name"] = Path.GetFileName(Directory.GetCurrentDirectory());
                        cfg["image"] = $"k3d-registry.localhost:5000/{cfg["name"]}";
                        cfg["imageTag"] = "local";
                    }

                    // create AutoGitOps files
                    if (!Directory.Exists("autogitops"))
                    {
                        Directory.CreateDirectory("autogitops");
                    }

                    string ago;

                    if (!File.Exists("autogitops/autogitops.json"))
                    {
                        ago = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "dotnet/autogitops.json"))
                            .Replace("{{gitops.name}}", cfg["name"].ToString())
                            .Replace("{{gitops.namespace}}", cfg["namespace"].ToString());
                        File.WriteAllText("autogitops/autogitops.json", ago);
                    }

                    ago = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "dotnet/app.yaml"));

                    if (File.Exists(TemplateFile))
                    {
                        ago = File.ReadAllText(TemplateFile);
                    }

                    ago = ago.Replace("{{gitops.name}}", cfg["name"].ToString())
                        .Replace("{{gitops.namespace}}", cfg["namespace"].ToString())
                        .Replace("{{gitops.version}}", cfg["version"].ToString())
                        .Replace("{{gitops.deploy}}", cfg["deploy"].ToString())
                        .Replace("{{gitops.imageName}}", cfg["imageName"].ToString())
                        .Replace("{{gitops.imageTag}}", cfg["imageTag"].ToString())
                        .Replace("{{gitops.port}}", cfg["port"].ToString())
                        .Replace("{{gitops.nodePort}}", cfg["nodePort"].ToString());
                    File.WriteAllText($"autogitops/{cfg["name"]}.yaml", ago);

                    if (!File.Exists("Dockerfile"))
                    {
                        ago = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "dotnet/Dockerfile"))
                            .Replace("{{gitops.port}}", cfg["port"].ToString())
                            .Replace("{{gitops.name}}", cfg["name"].ToString());
                        File.WriteAllText("Dockerfile", ago);
                    }

                    break;

                case "build":
                case "deploy":
                    string img = cfg["imageName"].ToString() + ":" + cfg["imageTag"].ToString();

                    if (DockerBuild(img))
                    {
                        if (DockerPush(img))
                        {
                            if (cmd == "deploy" && Directory.Exists(GitOpsDir))
                            {
                                File.Copy($"autogitops/{cfg["name"]}.yaml", Path.Combine(GitOpsDir, $"{cfg["name"]}.yaml"), true);
                                DoDeploy();

                                return 0;
                            }
                        }
                    }

                    break;

                default:
                    break;
            }

            return 0;
        }

        // copy a directory / tree
        private static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new (sourceDirName);

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException($"Source directory does not exist or could not be found: {sourceDirName}");
            }

            DirectoryInfo[] dirs = dir.GetDirectories();

            // If the destination directory doesn't exist, create it
            Directory.CreateDirectory(destDirName);

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string tempPath = Path.Combine(destDirName, file.Name);
                file.CopyTo(tempPath, true);
            }

            // If copying subdirectories, copy them and their contents to new location.
            if (copySubDirs)
            {
                foreach (DirectoryInfo subdir in dirs)
                {
                    string tempPath = Path.Combine(destDirName, subdir.Name);
                    DirectoryCopy(subdir.FullName, tempPath, copySubDirs);
                }
            }
        }

        // handle ago init commands
        private static int DoInit()
        {
            Console.WriteLine("AutoGitOps Initialized");

            return 0;
        }

        // handle ago add commands
        private static int DoAdd(string cmd)
        {
            if (cmd == "app")
            {
                if (Directory.Exists("autogitops") && File.Exists("autogitops/app.yaml"))
                {
                    File.Copy("autogitops/app.yaml", Path.Combine(GitOpsDir, "app.yaml"));
                    return 0;
                }
            }
            else
            {
                Directory.SetCurrentDirectory(AppContext.BaseDirectory);

                string bootstrapDir = Path.Combine(GitOpsDir, "bootstrap");

                if (!Directory.Exists(bootstrapDir))
                {
                    Directory.CreateDirectory(bootstrapDir);
                }

                if (Directory.Exists(bootstrapDir))
                {
                    File.Copy($"deploy/{cmd}.yaml", Path.Combine(bootstrapDir, $"{cmd}.yaml"), true);

                    return 0;
                }
            }

            Console.WriteLine($"{GitOpsDir} is missing");
            return 1;
        }

        // handle ago deploy commands
        private static int DoDeploy()
        {
            if (Directory.Exists(GitOpsDir))
            {
                Directory.SetCurrentDirectory(GitOpsDir);
                ExecGit("pull");

                if (HasGitChanges())
                {
                    ExecGit("add .");
                    ExecGit("commit -m AutoGitOps");
                    ExecGit("push");
                    FluxSync();
                }

                return 0;
            }

            Console.WriteLine("/workspaces/gitops repo is missing");
            return 1;
        }

        // exec git clone
        private static bool GitClone(string user, string pat, string repo)
        {
            const string runDir = "../run_autogitops";
            bool success;

            try
            {
                if (Directory.Exists(runDir))
                {
                    Directory.SetCurrentDirectory(runDir);
                    success = ExecGit("pull");
                }
                else
                {
                    success = ExecGit($"clone https://{user}:{pat}@github.com{repo} {runDir}");
                }

                if (success)
                {
                    Directory.SetCurrentDirectory(runDir);
                }

                return success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GitClone exception: {ex.Message}");
                return false;
            }
        }

        // check git repo for changes
        private static bool HasGitChanges()
        {
            string command = "status -s";

            try
            {
                using System.Diagnostics.Process git = new ();
                git.StartInfo.FileName = "git";
                git.StartInfo.Arguments = command;
                git.StartInfo.UseShellExecute = false;
                git.StartInfo.RedirectStandardOutput = true;
                git.Start();

                string output = git.StandardOutput.ReadToEnd();

                git.WaitForExit();

                return git.ExitCode == 0 && !string.IsNullOrWhiteSpace(output);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ExecGit exception: git {command}\n{ex.Message}");
                return false;
            }
        }

        // exec dotnet new
        private static bool DotNetNew()
        {
            try
            {
                using System.Diagnostics.Process git = new ();
                git.StartInfo.FileName = "dotnet";
                git.StartInfo.Arguments = "new webapi --no-https";
                git.StartInfo.UseShellExecute = false;
                git.StartInfo.RedirectStandardOutput = false;
                git.Start();
                git.WaitForExit();

                return git.ExitCode == 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DockerBuild exception: {ex.Message}");
                return false;
            }
        }

        // exec docker build
        private static bool DockerBuild(string command)
        {
            try
            {
                using System.Diagnostics.Process git = new ();
                git.StartInfo.FileName = "docker";
                git.StartInfo.Arguments = $"build . -t {command}";
                git.StartInfo.UseShellExecute = false;
                git.StartInfo.RedirectStandardOutput = false;
                git.Start();
                git.WaitForExit();

                return git.ExitCode == 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DockerBuild exception: {ex.Message}");
                return false;
            }
        }

        // exec docker push
        private static bool DockerPush(string command)
        {
            try
            {
                using System.Diagnostics.Process git = new ();
                git.StartInfo.FileName = "docker";
                git.StartInfo.Arguments = $"push {command}";
                git.StartInfo.UseShellExecute = false;
                git.StartInfo.RedirectStandardOutput = false;
                git.Start();
                git.WaitForExit();

                return git.ExitCode == 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DockerBuild exception: {ex.Message}");
                return false;
            }
        }

        // exec git commands
        private static bool ExecGit(string command)
        {
            try
            {
                using System.Diagnostics.Process git = new ();
                git.StartInfo.FileName = "git";
                git.StartInfo.Arguments = command;
                git.StartInfo.UseShellExecute = false;
                git.StartInfo.RedirectStandardOutput = false;
                git.Start();
                git.WaitForExit();

                return git.ExitCode == 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ExecGit exception: git {command}\n{ex.Message}");
                return false;
            }
        }

        // exec fluxctl sync
        private static void FluxSync()
        {
            try
            {
                using System.Diagnostics.Process git = new ();
                git.StartInfo.FileName = "fluxctl";
                git.StartInfo.Arguments = "sync";
                git.StartInfo.UseShellExecute = false;
                git.StartInfo.RedirectStandardOutput = false;
                git.Start();
                git.WaitForExit();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FluxSync exception: {ex.Message}");
            }
        }

        // read and validate App Config from autogitops.json
        private static Dictionary<string, object> ReadAppConfig()
        {
            string file = $"{Config.TemplateDir}/autogitops.json";

            if (!File.Exists(file))
            {
                Console.WriteLine("autogitops.json file not found");
                return null;
            }

            try
            {
                // deserialze the json
                Dictionary<string, object> cfg = JsonSerializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(file), JsonOptions);

                bool err = false;

                // check for required fields
                if (!cfg.ContainsKey("name") || string.IsNullOrWhiteSpace(cfg["name"].ToString()))
                {
                    Console.WriteLine("Invalid autogitops.json - name is a required field");
                    err = true;
                }

                if (!cfg.ContainsKey("namespace") || string.IsNullOrWhiteSpace(cfg["namespace"].ToString()))
                {
                    Console.WriteLine("Invalid autogitops.json - namespace is a required field");
                    err = true;
                }

                if (!cfg.ContainsKey("targets"))
                {
                    Console.WriteLine("Invalid autogitops.json - targets is required array");
                    err = true;
                }

                if (err)
                {
                    return null;
                }

                string t = cfg["targets"].ToString().Trim();

                if (string.IsNullOrWhiteSpace(t) ||
                    !t.StartsWith('[') ||
                    !t.EndsWith(']'))
                {
                    Console.WriteLine("Invalid autogitops.json - targets is required array");
                    return null;
                }

                // extract and remove the targets
                targets = JsonSerializer.Deserialize<List<string>>(t);
                cfg.Remove("targets");

                // add version and deploy if missing
                if (!cfg.ContainsKey("version"))
                {
                    cfg.Add("version", Config.ContainerVersion);
                }

                if (!cfg.ContainsKey("deploy"))
                {
                    cfg.Add("deploy", Now.ToString("yy-MM-dd-HH-mm-ss"));
                }

                return cfg;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception reading autogitops.json: {ex.Message}");
                return null;
            }
        }

        // set working directory to /deploy
        private static bool SetDeployDir()
        {
            string dir = Config.OutputDir;

            // handle running in debugger
            if (!Directory.Exists(dir))
            {
                Console.WriteLine("deploy directory doesn't exist");
                return false;
            }

            Directory.SetCurrentDirectory(dir);

            return true;
        }

        // delete existing deployments
        private static void DeleteDeployments()
        {
            // delete all deployment files
            foreach (string target in Directory.EnumerateDirectories("."))
            {
                string fn = $"{target}/{appConfig["namespace"]}";

                if (Directory.Exists(fn))
                {
                    fn = $"{target}/{appConfig["namespace"]}/{appConfig["name"]}.yaml";

                    if (File.Exists(fn))
                    {
                        File.Delete(fn);
                    }
                }
            }
        }

        // create new deployments
        private static bool CreateDeployments(bool dryRun)
        {
            Dictionary<string, object> config;
            string fileName;
            string yaml;
            string[] lines;

            try
            {
                if (targets.Count > 0)
                {
                    string templFile;

                    foreach (string target in targets)
                    {
                        // load config.json for target cluster
                        config = JsonSerializer.Deserialize<Dictionary<string, object>>(File.ReadAllText($"{target}/config.json"), JsonOptions);

                        templFile = "autogitops.yaml";

                        // check cluster config for template file name
                        if (config.ContainsKey("template"))
                        {
                            templFile = config["template"].ToString().Trim();

                            if (string.IsNullOrWhiteSpace(templFile))
                            {
                                Console.WriteLine($"Invalid template in {target}");
                                return false;
                            }
                        }

                        // read the deployment template
                        yaml = File.ReadAllText($"{Config.TemplateDir}/{templFile}");

                        // replace each app config value
                        foreach (KeyValuePair<string, object> kv in appConfig)
                        {
                            yaml = yaml.Replace("{{gitops." + kv.Key + "}}", kv.Value.ToString())
                                .Replace("{{ gitops." + kv.Key + " }}", kv.Value.ToString());
                        }

                        // replace each cluster config value
                        foreach (KeyValuePair<string, object> kv in config)
                        {
                            yaml = yaml.Replace("{{gitops.config." + kv.Key + "}}", kv.Value.ToString())
                                .Replace("{{ gitops.config." + kv.Key + " }}", kv.Value.ToString());
                        }

                        // check the yaml
                        lines = yaml.Split('\n');
                        bool err = false;

                        foreach (string line in lines)
                        {
                            if (line.Contains("{{gitops.") || line.Contains("{{ gitops."))
                            {
                                if (!err)
                                {
                                    Console.WriteLine("Error in gitops.yaml");
                                }

                                err = true;

                                Console.WriteLine(line);
                            }
                        }

                        if (err)
                        {
                            return false;
                        }

                        if (!dryRun)
                        {
                            // create the namespace directory
                            fileName = $"{target}/{appConfig["namespace"]}";
                            if (!Directory.Exists(fileName))
                            {
                                Directory.CreateDirectory(fileName);
                            }

                            // create namespace.yaml
                            fileName = $"{fileName}/namespace.yaml";
                            if (!File.Exists(fileName))
                            {
                                File.WriteAllText(fileName, $"apiVersion: v1\nkind: Namespace\nmetadata:\n  labels:\n    name: {appConfig["namespace"]}\n  name: {appConfig["namespace"]}\n");
                            }

                            // write file
                            fileName = $"{target}/{appConfig["namespace"]}/{appConfig["name"]}.yaml";
                            File.WriteAllText(fileName, yaml);
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception creating deployments: {ex.Message}");
            }

            return false;
        }

        // display Ascii Art
        private static void DisplayAsciiArt(string[] args)
        {
            if (args != null)
            {
                ReadOnlySpan<string> cmd = new (args);

                if (!cmd.Contains("--version") &&
                    (cmd.Contains("-h") ||
                    cmd.Contains("--help") ||
                    cmd.Contains("--dry-run")))
                {
                    string path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    string file = $"{path}/Core/ascii-art.txt";

                    try
                    {
                        if (File.Exists(file))
                        {
                            string txt = File.ReadAllText(file);

                            if (!string.IsNullOrWhiteSpace(txt))
                            {
                                txt = txt.Replace("\r", string.Empty);
                                string[] lines = txt.Split('\n');

                                foreach (string line in lines)
                                {
                                    Console.ForegroundColor = ConsoleColor.DarkBlue;
                                    if (line.Length > 26)
                                    {
                                        Console.Write(line[0..25]);
                                        Console.ForegroundColor = ConsoleColor.DarkMagenta;
                                        Console.WriteLine(line[25..]);
                                    }
                                    else
                                    {
                                        Console.WriteLine(line);
                                    }
                                }

                                Console.ResetColor();
                            }
                        }
                    }
                    catch
                    {
                        // ignore any errors
                    }
                }
            }
        }
    }
}
