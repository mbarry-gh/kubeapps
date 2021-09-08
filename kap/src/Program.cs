// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Kube.Apps
{
    /// <summary>
    /// Main application class
    /// </summary>
    public sealed partial class App
    {
        /// <summary>
        /// Main entry point
        /// </summary>
        /// <param name="args">Command Line Parameters</param>
        /// <returns>0 on success</returns>
        public static int Main(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                args = new string[] { "--help" };
            }

            DisplayAsciiArt(args);

            InitKap();

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
                root.Handler = CommandHandler.Create<Config>(RunApp);
                return root.Invoke(args);
            }

            // verify GitOps repo exists
            if (!Directory.Exists(Dirs.GitOpsBase))
            {
                Console.Error.WriteLine($"{Dirs.GitOpsBase} not found\n\n  Please clone a git repo to {Dirs.GitOpsBase} and try again");
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
                Console.WriteLine("unable to create GitOps directory");
                return 1;
            }

            // handle the commands
            if (res.RootCommandResult.Children.Count > 0)
            {
                switch (res.RootCommandResult.Children[0].Symbol.Name)
                {
                    case "add":
                        return DoAdd(res);

                    case "build":
                        return DoBuild();

                    case "check":
                        return DoCheck();

                    case "deploy":
                        return DoDeploy();

                    case "init":
                        return DoInit();

                    case "logs":
                        return DoLogs();

                    case "new":
                        return DoNew(res);

                    case "remove":
                        return DoRemove(res);

                    default:
                        break;
                }
            }

            // this should never happen
            Console.WriteLine("command line handler not found\n\n  Please report this as a bug");
            return 1;
        }

        private static void InitKap()
        {
            Dirs.IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            Dirs.KapBase = AppContext.BaseDirectory;
            Dirs.KapStart = Directory.GetCurrentDirectory();
            Dirs.KapHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), Dirs.HomeSubDir);

            if (!Directory.Exists(Dirs.KapHome))
            {
                Directory.CreateDirectory(Dirs.KapHome);

                DirectoryCopy(Path.Combine(Dirs.KapBase, "bootstrap"), Dirs.KapBootstrapDir, true);
                DirectoryCopy(Path.Combine(Dirs.KapBase, "dotnet"), Dirs.KapDotnetDir, true);
            }
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

        private static bool DoCheck(string nodePort, string path)
        {
            string command = $"localhost:{nodePort}/{path}".Replace("//", "/");

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
                    string file = $"{path}/files/ascii-art.txt";

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
                                    Console.ForegroundColor = ConsoleColor.DarkMagenta;
                                    Console.WriteLine(line);
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
