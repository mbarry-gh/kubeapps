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

        // handle app commands
        private static int DoApp(ParseResult parse)
        {
            string cmd = parse.CommandResult.Command.Name;

            if (cmd == "init" && !File.Exists(Dirs.ConfigFile))
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
                case "logs":
                    string args = $"logs -n {KapConfig["namespace"]} -l app={KapConfig["name"]}";

                    ShellExec.Run("kubectl", args);
                    break;

                case "remove":
                    // delete the file from GitOps
                    string file = Path.Combine(Dirs.GitOpsDir, $"{KapConfig["namespace"]}-{KapConfig["name"]}.yaml");

                    if (File.Exists(file))
                    {
                        File.Delete(file);
                        DoDeploy();
                    }

                    break;

                case "init":
                    return DoInit();

                case "check":
                    if (KapConfig.ContainsKey("nodePort") && KapConfig.ContainsKey("readyProbe"))
                    {
                        DoCheck(KapConfig["nodePort"].ToString(), KapConfig["readyProbe"].ToString());
                    }

                    break;

                case "build":
                case "deploy":
                    string img = KapConfig["imageName"].ToString() + ":" + KapConfig["imageTag"].ToString();

                    if (ShellExec.Run("docker", $"build . -t {img}"))
                    {
                        if (ShellExec.Run("docker", $"push {img}"))
                        {
                            if (cmd == "deploy" && Directory.Exists(Dirs.GitOpsDir))
{
                                File.Copy($"kubeapps/{KapConfig["namespace"]}-{KapConfig["name"]}.yaml", Path.Combine(Dirs.GitOpsDir, $"{KapConfig["namespace"]}-{KapConfig["name"]}.yaml"), true);
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

        private static int DoInit()
        {
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
                    .Replace("{{gitops.readyProbe}}", KapConfig["readyProbe"].ToString());

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
                .Replace("{{gitops.readyProbe}}", KapConfig["readyProbe"].ToString());

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
                        KapConfig["imageName"] = KapConfig["name"];

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
        private static int DoAdd(string cmd)
        {
            if (Directory.Exists(Dirs.GitOpsBase))
            {
                if (!Directory.Exists(Dirs.GitOpsDir))
                {
                    Directory.CreateDirectory(Dirs.GitOpsDir);
                }

                if (!Directory.Exists(Dirs.GitOpsBootstrapDir))
                {
                    Directory.CreateDirectory(Dirs.GitOpsBootstrapDir);
                }

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

                    return 0;
                }
            }

            Console.WriteLine($"{Dirs.GitOpsBase} is missing");
            return 1;
        }

        // handle deploy commands
        private static int DoDeploy()
        {
            if (Directory.Exists(Dirs.GitOpsDir))
            {
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

        // handle remove commands
        private static int DoRemove(string cmd)
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

                return 0;
            }

            Console.WriteLine($"{Dirs.GitOpsDir} is missing");
            return 1;
        }
    }
}
