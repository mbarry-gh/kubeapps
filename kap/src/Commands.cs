// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Kube.Apps
{
    /// <summary>
    /// Command Handlers
    /// </summary>
    public sealed partial class App
    {
        private static readonly Dictionary<string, object> KapConfig = Config.ReadKapConfig();

        private static int DoInit()
        {
            if (!File.Exists(Dirs.ConfigFile))
            {
                IEnumerable<string> files = Directory.EnumerateFiles(".", "*.csproj");

                if (!files.Any())
                {
                    Console.WriteLine("Could not find .csproj file");
                    return 1;
                }
            }

            // create KubeApps files
            if (!Directory.Exists("kubeapps"))
            {
                Directory.CreateDirectory("kubeapps");
            }

            string templ;

            if (!File.Exists(Dirs.ConfigFile))
            {
                templ = File.ReadAllText(Path.Combine(Dirs.KapDotnetDir, Dirs.DotnetConfig))
                    .Replace("{{gitops.name}}", KapConfig["name"].ToString())
                    .Replace("{{gitops.namespace}}", KapConfig["namespace"].ToString())
                    .Replace("{{gitops.imageName}}", KapConfig["imageName"].ToString())
                    .Replace("{{gitops.imageTag}}", KapConfig["imageTag"].ToString())
                    .Replace("{{gitops.port}}", KapConfig["port"].ToString())
                    .Replace("{{gitops.nodePort}}", KapConfig["nodePort"].ToString())
                    .Replace("{{gitops.livenessProbe}}", KapConfig["livenessProbe"].ToString())
                    .Replace("{{gitops.readinessProbe}}", KapConfig["readinessProbe"].ToString());

                File.WriteAllText(Dirs.ConfigFile, templ);
            }

            templ = File.ReadAllText(Path.Combine(Dirs.KapDotnetDir, Dirs.DotnetTemplate));

            if (File.Exists(Dirs.TemplateFile))
            {
                templ = File.ReadAllText(Dirs.TemplateFile);
            }

            templ = templ.Replace("{{gitops.name}}", KapConfig["name"].ToString())
                .Replace("{{gitops.namespace}}", KapConfig["namespace"].ToString())
                .Replace("{{gitops.version}}", KapConfig["version"].ToString())
                .Replace("{{gitops.deploy}}", KapConfig["deploy"].ToString())
                .Replace("{{gitops.imageName}}", KapConfig["imageName"].ToString())
                .Replace("{{gitops.imageTag}}", KapConfig["imageTag"].ToString())
                .Replace("{{gitops.port}}", KapConfig["port"].ToString())
                .Replace("{{gitops.nodePort}}", KapConfig["nodePort"].ToString())
                .Replace("{{gitops.livenessProbe}}", KapConfig["livenessProbe"].ToString())
                .Replace("{{gitops.readinessProbe}}", KapConfig["readinessProbe"].ToString());

            File.WriteAllText($"kubeapps/{KapConfig["namespace"]}-{KapConfig["name"]}.yaml", templ);

            if (!File.Exists("Dockerfile"))
            {
                templ = File.ReadAllText(Path.Combine(Dirs.KapDotnetDir, "Dockerfile"))
                    .Replace("{{gitops.port}}", KapConfig["port"].ToString())
                    .Replace("{{gitops.name}}", KapConfig["name"].ToString());

                File.WriteAllText("Dockerfile", templ);
            }

            return 0;
        }

        // handle new app commands
        private static int DoNew(ParseResult parse)
        {
            string cmd = parse.CommandResult.Command.Name;

            switch (cmd)
            {
                case "dotnet":
                    if (parse.UnmatchedTokens.Count > 0)
                    {
                        string dir = parse.UnmatchedTokens[0].Trim();
                        Directory.CreateDirectory(dir);
                        Directory.SetCurrentDirectory(dir);
                        KapConfig["name"] = dir;

                        if (parse.UnmatchedTokens.Count > 2 &&
                            parse.UnmatchedTokens[1] == "--np" &&
                            int.TryParse(parse.UnmatchedTokens[2], out int np))
                        {
                            KapConfig["nodePort"] = np.ToString();
                        }
                    }

                    ShellExec.Run("dotnet", "new webapi --no-https");

                    return DoInit();

                default:
                    break;
            }

            return 0;
        }

        // handle add commands
        private static int DoAdd(ParseResult res)
        {
            string cmd = string.Empty;

            if (res.UnmatchedTokens.Count > 0)
            {
                cmd = res.UnmatchedTokens[0];
            }

            if (Directory.Exists(Dirs.GitOpsBase))
            {
                if (string.IsNullOrWhiteSpace(cmd))
                {
                    if (!Dirs.IsAppDir)
                    {
                        DoInit();
                    }

                    if (Dirs.IsAppDir)
                    {
                        // copy the file to GitOps
                        string file = Path.Combine("kubeapps", $"{KapConfig["namespace"]}-{KapConfig["name"]}.yaml");

                        if (!File.Exists(file))
                        {
                            DoInit();
                        }

                        if (File.Exists(file))
                        {
                            File.Copy(file, Path.Combine(Dirs.GitOpsDir, $"{KapConfig["namespace"]}-{KapConfig["name"]}.yaml"), true);
                        }
                        else
                        {
                            Console.WriteLine($"unable to find or generate {file}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Could not find kubeapps/config.json");
                        return 1;
                    }
                }
                else
                {
                    if (Directory.Exists(Dirs.GitOpsBootstrapDir))
                    {
                        if (cmd == "all")
                        {
                            IEnumerable<string> files = Directory.EnumerateFiles(Dirs.KapBootstrapDir, "*.yaml");

                            foreach (string f in files)
                            {
                                File.Copy(f, Path.Combine(Dirs.GitOpsBootstrapDir, Path.GetFileName(f)), true);
                            }
                        }
                        else
                        {
                            File.Copy(Path.Combine(Dirs.KapBootstrapDir, $"{cmd}.yaml"), Path.Combine(Dirs.GitOpsBootstrapDir, $"{cmd}.yaml"), true);
                        }
                    }
                }

                return 0;
            }

            Console.WriteLine($"{Dirs.GitOpsBase} is missing");
            return 1;
        }

        // handle deploy commands
        private static int DoDeploy()
        {
            if (Directory.Exists(Dirs.GitOpsDir))
            {
                // build the app if possible
                if (Dirs.IsAppDir && File.Exists("./Dockerfile"))
                {
                    DoBuild();
                }

                Directory.SetCurrentDirectory(Dirs.GitOpsDir);
                ShellExec.Run("git", "pull");

                if (ShellExec.HasGitChanges())
                {
                    ShellExec.Run("git", "add .");
                    ShellExec.Run("git", "commit -m \"KubeApps Deploy\"");
                    ShellExec.Run("git", "push");
                    ShellExec.Run("fluxctl", "sync");
                }

                return 0;
            }

            Console.WriteLine($"{Dirs.GitOpsDir} repo is missing");
            return 1;
        }

        // docker buil the app
        private static int DoBuild()
        {
            if (Dirs.IsAppDir)
            {
                string img = KapConfig["imageName"].ToString() + ":" + KapConfig["imageTag"].ToString();

                if (ShellExec.Run("docker", $"build . -t {img}"))
                {
                    if (ShellExec.Run("docker", $"push {img}"))
                    {
                        return 0;
                    }
                }

                Console.WriteLine("docker build failed");
                return 1;
            }

            Console.WriteLine("./kubeapps/config.json not found");
            return 1;
        }

        // check the app endpoint
        private static int DoCheck()
        {
            if (Dirs.IsAppDir)
            {
                if (KapConfig.ContainsKey("nodePort") && KapConfig.ContainsKey("readinessProbe"))
                {
                    DoCheck(KapConfig["nodePort"].ToString(), KapConfig["readinessProbe"].ToString());
                }
            }
            else
            {
                Console.WriteLine("./kubeapps/config.json not found");
            }

            return 0;
        }

        // show the kubernetes logs for the app
        private static int DoLogs()
        {
            if (Dirs.IsAppDir)
            {
                string cmd = $"logs -n {KapConfig["namespace"]} -l app={KapConfig["name"]}";
                ShellExec.Run("kubectl", cmd);
            }
            else
            {
                Console.WriteLine("./kubeapps/config.json not found");
            }

            return 0;
        }

        // handle remove commands
        private static int DoRemove(ParseResult res)
        {
            string cmd = string.Empty;

            if (res.UnmatchedTokens.Count > 0)
            {
                cmd = res.UnmatchedTokens[0];
            }

            if (Directory.Exists(Dirs.GitOpsDir))
            {
                if (string.IsNullOrWhiteSpace(cmd))
                {
                    if (Dirs.IsAppDir)
                    {
                        // delete the file from GitOps
                        string file = Path.Combine(Dirs.GitOpsDir, $"{KapConfig["namespace"]}-{KapConfig["name"]}.yaml");

                        if (File.Exists(file))
                        {
                            File.Delete(file);
                        }
                    }
                }
                else
                {
                    if (Directory.Exists(Dirs.GitOpsBootstrapDir))
                    {
                        if (cmd == "all")
                        {
                            IEnumerable<string> files = Directory.EnumerateFiles(Dirs.GitOpsBootstrapDir, "*.*");

                            foreach (string f in files)
                            {
                                File.Delete(f);
                            }
                        }
                        else
                        {
                            if (File.Exists(Path.Combine(Dirs.GitOpsBootstrapDir, $"{cmd}.yaml")))
                            {
                                File.Delete(Path.Combine(Dirs.GitOpsBootstrapDir, $"{cmd}.yaml"));
                            }
                        }
                    }
                }

                return 0;
            }

            Console.WriteLine($"{Dirs.GitOpsDir} is missing");
            return 1;
        }
    }
}
