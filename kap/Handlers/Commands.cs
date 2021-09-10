// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;

namespace Kube.Apps
{
    /// <summary>
    /// Command Handlers
    /// </summary>
    public sealed partial class Commands
    {
        // command names
        public const string All = "all";
        public const string Add = "add";
        public const string Bootstrap = "bootstrap";
        public const string Build = "build";
        public const string Check = "check";
        public const string ListPods = "list";
        public const string Sync = "sync";
        public const string Init = "init";
        public const string Logs = "logs";
        public const string New = "new";
        public const string DotNet = "dotnet";
        public const string Remove = "remove";
        public const string Config = "config";
        public const string Reset = "reset";
        public const string Update = "update";
        public const string List = "list";

        // template field names
        public const string Name = "name";
        public const string Namespace = "namespace";
        public const string ImageName = "imageName";
        public const string ImageTag = "imageTag";
        public const string Port = "port";
        public const string NodePort = "nodePort";
        public const string LivenessProbe = "livenessProbe";
        public const string ReadinessProbe = "readinessProbe";
        public const string Version = "version";
        public const string Deploy = "deploy";

        // other known names
        public const string Dockerfile = "Dockerfile";
        public const string Localhost = "localhost";
        public const string CsprojSearch = "*.csproj";

        // read / generate app config
        public Dictionary<string, object> KapConfig { get; } = AppConfig.ReadKapConfig();

        // template replace value
        public static string GitOpsValue(string name)
        {
            return $"{{{{gitops.{name}}}}}";
        }

#pragma warning disable CA1822 // Interface consistency

        // run the command processor
        public int Run(string[] args)
        {
            // build and parse the command line args
            RootCommand root = BuildRootCommand();
            ParseResult res = root.Parse(args);

            // short circuit errors, help and version
            if (res.Errors.Count > 0 ||
                res.RootCommandResult.Children.Count == 0 ||
                args.Contains("-h") ||
                args.Contains("--help") ||
                args.Contains("--version"))
            {
                // run the app
                root.Handler = CommandHandler.Create<AppConfig>(RunApp);
                return root.Invoke(args);
            }

            // verify GitOps repo exists
            if (!Directory.Exists(Dirs.GitOpsBase))
            {
                Console.Error.WriteLine(Messages.GitOpsBaseNotFound);
                return 1;
            }

            // create directories if needed
            if (!Directory.Exists(Dirs.GitOpsDir))
            {
                Directory.CreateDirectory(Dirs.GitOpsDir);
            }

            if (!Directory.Exists(Dirs.GitOpsBootstrapDir))
            {
                Directory.CreateDirectory(Dirs.GitOpsBootstrapDir);
            }

            // fail if directories don't exist
            if (!Directory.Exists(Dirs.GitOpsDir) || !Directory.Exists(Dirs.GitOpsBootstrapDir))
            {
                Console.WriteLine(Messages.UnableCreateDir);
                return 1;
            }

            // handle the commands
            if (res.RootCommandResult.Children.Count > 0)
            {
                switch (res.RootCommandResult.Children[0].Symbol.Name)
                {
                    case Commands.Add:
                        return DoAdd(string.Empty);

                    case Commands.Bootstrap:
                        switch (res.RootCommandResult.Children[0].Children[0].Symbol.Name)
                        {
                            case Add:
                                return DoAdd(res.CommandResult.Command.Name);

                            case Remove:
                                return DoRemove(res.CommandResult.Command.Name);

                            default:
                                break;
                        }

                        break;

                    case Build:
                        return DoBuild();

                    case Check:
                        return DoCheck();

                    case Config:
                        return DoConfig(res.RootCommandResult.Children[0].Children[0].Symbol.Name);

                    case Sync:
                        return DoSync();

                    case Init:
                        return DoInit();

                    case ListPods:
                        return ShellExec.Run(ShellExec.Kubectl, "get pods -A") ? 0 : 1;

                    case Logs:
                        return DoLogs();
                    case New:
                        return DoNew(res);

                    case Remove:
                    case "rm":
                        return DoRemove(string.Empty);

                    default:
                        break;
                }
            }

            // this should never happen
            Console.WriteLine(Messages.HandlerNotFound);
            return 1;
        }

        // handle config
        public int DoConfig(string cmd)
        {
            Directory.SetCurrentDirectory(Dirs.KapHome);

            // reset and clean the git repo
            if (cmd == Commands.Reset)
            {
                ShellExec.Run(ShellExec.Git, "clean -f");
                ShellExec.Run(ShellExec.Git, "reset --hard");
            }

            // git pull the latest updates
            ShellExec.Run(ShellExec.Git, "pull");

            return 0;
        }

        // initialize KubeApps for the app
        public int DoInit()
        {
            if (!File.Exists(Dirs.ConfigFile))
            {
                IEnumerable<string> files = Directory.EnumerateFiles(".", CsprojSearch);

                if (!files.Any())
                {
                    Console.WriteLine("Could not find .csproj file");
                    return 1;
                }
            }

            // create KubeApps files
            if (!Directory.Exists(Dirs.KubeAppDir))
            {
                Directory.CreateDirectory(Dirs.KubeAppDir);
            }

            string templ;

            if (!File.Exists(Dirs.ConfigFile))
            {
                templ = File.ReadAllText(Path.Combine(Dirs.KapDotnetDir, Dirs.DotnetConfig))
                    .Replace($"{GitOpsValue(Name)}", KapConfig[Name].ToString())
                    .Replace($"{GitOpsValue(Namespace)}", KapConfig[Namespace].ToString())
                    .Replace($"{GitOpsValue(ImageName)}", KapConfig[ImageName].ToString())
                    .Replace($"{GitOpsValue(ImageTag)}", KapConfig[ImageTag].ToString())
                    .Replace($"{GitOpsValue(Port)}", KapConfig[Port].ToString())
                    .Replace($"{GitOpsValue(NodePort)}", KapConfig[NodePort].ToString())
                    .Replace($"{GitOpsValue(LivenessProbe)}", KapConfig[LivenessProbe].ToString())
                    .Replace($"{GitOpsValue(ReadinessProbe)}", KapConfig[ReadinessProbe].ToString());

                File.WriteAllText(Dirs.ConfigFile, templ);
            }

            templ = File.ReadAllText(Path.Combine(Dirs.KapDotnetDir, Dirs.DotnetTemplate));

            if (File.Exists(Dirs.TemplateFile))
            {
                templ = File.ReadAllText(Dirs.TemplateFile);
            }

            templ = templ.Replace($"{GitOpsValue(Name)}", KapConfig[Name].ToString())
                .Replace($"{GitOpsValue(Namespace)}", KapConfig[Namespace].ToString())
                .Replace($"{GitOpsValue(Version)}", KapConfig[Version].ToString())
                .Replace($"{GitOpsValue(Deploy)}", KapConfig[Deploy].ToString())
                .Replace($"{GitOpsValue(ImageName)}", KapConfig[ImageName].ToString())
                .Replace($"{GitOpsValue(ImageTag)}", KapConfig[ImageTag].ToString())
                .Replace($"{GitOpsValue(Port)}", KapConfig[Port].ToString())
                .Replace($"{GitOpsValue(NodePort)}", KapConfig[NodePort].ToString())
                .Replace($"{GitOpsValue(LivenessProbe)}", KapConfig[LivenessProbe].ToString())
                .Replace($"{GitOpsValue(ReadinessProbe)}", KapConfig[ReadinessProbe].ToString());

            File.WriteAllText($"{Dirs.KubeAppDir}/{KapConfig[Namespace]}-{KapConfig[Name]}.yaml", templ);

            if (!File.Exists(Dockerfile))
            {
                templ = File.ReadAllText(Path.Combine(Dirs.KapDotnetDir, Dockerfile))
                    .Replace($"{GitOpsValue(Port)}", KapConfig[Port].ToString())
                    .Replace($"{GitOpsValue(Name)}", KapConfig[Name].ToString());

                File.WriteAllText(Dockerfile, templ);
            }

            return 0;
        }

        // create and initialize a new app (dotnet only at this point)
        public int DoNew(ParseResult parse)
        {
            string cmd = parse.CommandResult.Command.Name;

            switch (cmd)
            {
                case Commands.DotNet:
                    if (parse.UnmatchedTokens.Count > 0)
                    {
                        string dir = parse.UnmatchedTokens[0].Trim();
                        Directory.CreateDirectory(dir);
                        Directory.SetCurrentDirectory(dir);
                        KapConfig[Name] = dir;

                        if (parse.UnmatchedTokens.Count > 2 &&
                            parse.UnmatchedTokens[1] == "--node-port" &&
                            int.TryParse(parse.UnmatchedTokens[2], out int np))
                        {
                            KapConfig[NodePort] = np.ToString();
                        }
                    }

                    ShellExec.Run(ShellExec.DotNet, "new webapi --no-https");

                    return DoInit();

                default:
                    break;
            }

            return 0;
        }

        // add the app to GitOps repo
        public int DoAdd(string cmd)
        {
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
                        string file = Path.Combine(Dirs.KubeAppDir, $"{KapConfig[Namespace]}-{KapConfig[Name]}.yaml");

                        if (!File.Exists(file))
                        {
                            DoInit();
                        }

                        // build the app if possible
                        if (File.Exists(Dockerfile))
                        {
                            DoBuild();
                        }

                        if (File.Exists(file))
                        {
                            File.Copy(file, Path.Combine(Dirs.GitOpsDir, $"{KapConfig[Namespace]}-{KapConfig[Name]}.yaml"), true);
                        }
                        else
                        {
                            Console.WriteLine($"unable to find or generate {file}");
                        }
                    }
                    else
                    {
                        Console.WriteLine(Messages.ConfigFileNotFound);
                        return 1;
                    }
                }
                else
                {
                    if (Directory.Exists(Dirs.GitOpsBootstrapDir))
                    {
                        if (cmd == "all")
                        {
                            IEnumerable<string> dirs = Directory.EnumerateDirectories(Dirs.KapBootstrapDir);

                            foreach (string dir in dirs)
                            {
                                DirectoryCopy(dir, Path.Combine(Dirs.GitOpsBootstrapDir, Path.GetFileName(dir)), true);
                            }
                        }
                        else
                        {
                            string dir = Path.Combine(Dirs.KapBootstrapDir, $"{cmd}");

                            if (Directory.Exists(dir))
                            {
                                DirectoryCopy(dir, Path.Combine(Dirs.GitOpsBootstrapDir, Path.GetFileName(dir)), true);
                            }
                        }
                    }
                }

                return 0;
            }

            Console.WriteLine($"{Dirs.GitOpsBase} is missing");
            return 1;
        }

        // synchronize git repo and run fluxctl sync
        public int DoSync()
        {
            if (Directory.Exists(Dirs.GitOpsDir))
            {
                Directory.SetCurrentDirectory(Dirs.GitOpsDir);
                ShellExec.Run(ShellExec.Git, "pull");

                if (ShellExec.HasGitChanges())
                {
                    ShellExec.Run(ShellExec.Git, "add .");
                    ShellExec.Run(ShellExec.Git, "commit -m \"KubeApps Deploy\"");
                    ShellExec.Run(ShellExec.Git, "push");
                    ShellExec.Run(ShellExec.Flux, "sync");
                }
            }

            return 0;
        }

        // docker build the app
        public int DoBuild()
        {
            if (Dirs.IsAppDir)
            {
                string img = KapConfig[ImageName].ToString() + ":" + KapConfig[ImageTag].ToString();

                if (ShellExec.Run(ShellExec.Docker, $"build . -t {img}"))
                {
                    if (ShellExec.Run(ShellExec.Docker, $"push {img}"))
                    {
                        return 0;
                    }
                }

                Console.WriteLine("docker build failed");
                return 1;
            }

            Console.WriteLine(Messages.ConfigFileNotFound);
            return 1;
        }

        // check the app endpoint
        public int DoCheck()
        {
            if (Dirs.IsAppDir)
            {
                if (KapConfig.ContainsKey(NodePort) && KapConfig.ContainsKey(ReadinessProbe))
                {
                    DoCheck(KapConfig[NodePort].ToString(), KapConfig[ReadinessProbe].ToString());
                }
            }
            else
            {
                Console.WriteLine(Messages.ConfigFileNotFound);
            }

            return 0;
        }

        // show the kubernetes logs for the app
        public int DoLogs()
        {
            if (Dirs.IsAppDir)
            {
                string cmd = $"logs -n {KapConfig[Namespace]} -l app={KapConfig[Name]}";
                ShellExec.Run(ShellExec.Kubectl, cmd);
            }
            else
            {
                Console.WriteLine(Messages.ConfigFileNotFound);
            }

            return 0;
        }

        // remove the app or a bootstrap service
        public int DoRemove(string cmd)
        {
            if (Directory.Exists(Dirs.GitOpsDir))
            {
                if (string.IsNullOrWhiteSpace(cmd))
                {
                    if (Dirs.IsAppDir)
                    {
                        // delete the file from GitOps
                        string file = Path.Combine(Dirs.GitOpsDir, $"{KapConfig[Namespace]}-{KapConfig[Name]}.yaml");

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
                            IEnumerable<string> dirs = Directory.EnumerateDirectories(Dirs.GitOpsBootstrapDir);

                            foreach (string dir in dirs)
                            {
                                Directory.Delete(dir, true);
                            }
                        }
                        else
                        {
                            string dir = Path.Combine(Dirs.GitOpsBootstrapDir, $"{cmd}");

                            if (Directory.Exists(dir))
                            {
                                Directory.Delete(dir, true);
                            }
                        }
                    }
                }
            }

            return 0;
        }

        // DoCheck worker
        private bool DoCheck(string nodePort, string path)
        {
            string command = $"{Localhost}:{nodePort}/{path}".Replace("//", "/");

            Console.WriteLine(command);

            try
            {
                using System.Diagnostics.Process git = new ();
                git.StartInfo.FileName = "http";
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

        // copy a directory / tree
        private void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new (sourceDirName);

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException($"{Messages.SourceDirNotFound}: {sourceDirName}");
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

#pragma warning restore CA1822
    }
}
