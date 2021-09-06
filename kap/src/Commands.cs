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
    /// Command Handlers
    /// </summary>
    public sealed partial class App
    {
        // handle ago app commands
        private static int DoApp(ParseResult parse)
        {
            string cmd = parse.CommandResult.Command.Name;

            Dictionary<string, object> cfg = Config.ReadAgoConfig();

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
                    string args = $"logs -n {cfg["namespace"]} -l app={cfg["name"]}";

                    ShellExec.Run("kubectl", args);
                    break;

                case "remove":
                    // delete the file from GitOps
                    string file = Path.Combine(Dirs.GitOpsDir, $"{cfg["name"]}.yaml");

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
                        if (parse.UnmatchedTokens.Count > 0)
                        {
                            string dir = parse.UnmatchedTokens[0].Trim();
                            Directory.CreateDirectory(dir);
                            Directory.SetCurrentDirectory(dir);
                            cfg["name"] = dir;
                            cfg["imageName"] = $"k3d-registry.localhost:5000/{cfg["name"]}";
                        }

                        ShellExec.Run("dotnet", "new webapi --no-https");
                    }

                    // create KubeApps files
                    if (!Directory.Exists("kubeapps"))
                    {
                        Directory.CreateDirectory("kubeapps");
                    }

                    string ago;

                    if (!File.Exists(Dirs.ConfigFile))
                    {
                        ago = File.ReadAllText(Path.Combine(Dirs.KapDotnetDir, Dirs.DotnetConfig))
                            .Replace("{{gitops.name}}", cfg["name"].ToString())
                            .Replace("{{gitops.namespace}}", cfg["namespace"].ToString());
                        File.WriteAllText(Dirs.ConfigFile, ago);
                    }

                    ago = File.ReadAllText(Path.Combine(Dirs.KapDotnetDir, Dirs.DotnetTemplate));

                    if (File.Exists(Dirs.TemplateFile))
                    {
                        ago = File.ReadAllText(Dirs.TemplateFile);
                    }

                    ago = ago.Replace("{{gitops.name}}", cfg["name"].ToString())
                        .Replace("{{gitops.namespace}}", cfg["namespace"].ToString())
                        .Replace("{{gitops.version}}", cfg["version"].ToString())
                        .Replace("{{gitops.deploy}}", cfg["deploy"].ToString())
                        .Replace("{{gitops.imageName}}", cfg["imageName"].ToString())
                        .Replace("{{gitops.imageTag}}", cfg["imageTag"].ToString())
                        .Replace("{{gitops.port}}", cfg["port"].ToString())
                        .Replace("{{gitops.nodePort}}", cfg["nodePort"].ToString());
                    File.WriteAllText($"kubeapps/{cfg["name"]}.yaml", ago);

                    if (!File.Exists("Dockerfile"))
                    {
                        ago = File.ReadAllText(Path.Combine(Dirs.KapDotnetDir, "Dockerfile"))
                            .Replace("{{gitops.port}}", cfg["port"].ToString())
                            .Replace("{{gitops.name}}", cfg["name"].ToString());
                        File.WriteAllText("Dockerfile", ago);
                    }

                    break;

                case "check":
                    if (cfg.ContainsKey("nodePort") && cfg.ContainsKey("probePath"))
                    {
                        DoCheck(cfg["nodePort"].ToString(), cfg["probePath"].ToString());
                    }

                    break;

                case "build":
                case "deploy":
                    string img = cfg["imageName"].ToString() + ":" + cfg["imageTag"].ToString();

                    if (ShellExec.Run("docker", $"build . -t {img}"))
                    {
                        if (ShellExec.Run("docker", $"push {img}"))
                        {
                            if (cmd == "deploy" && Directory.Exists(Dirs.GitOpsDir))
                            {
                                File.Copy($"kubeapps/{cfg["name"]}.yaml", Path.Combine(Dirs.GitOpsDir, $"{cfg["name"]}.yaml"), true);
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

        // handle ago add commands
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

        // handle ago deploy commands
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

        // handle ago add commands
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
