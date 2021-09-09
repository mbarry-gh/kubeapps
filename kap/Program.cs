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

            if (args[0] == "help")
            {
                args[0] = "--help";
            }

            if (args[0] == "version")
            {
                args[0] = "--version";
            }

            DisplayAsciiArt(args);

            InitKap();

            return new Commands().Run(args);
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

                DirectoryCopy(Path.Combine(Dirs.KapBase, Commands.Bootstrap), Dirs.KapBootstrapDir, true);
                DirectoryCopy(Path.Combine(Dirs.KapBase, Commands.DotNet), Dirs.KapDotnetDir, true);
            }
        }

        // copy a directory / tree
        private static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
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

        // display Ascii Art
        private static void DisplayAsciiArt(string[] args)
        {
            if (args != null)
            {
                if (!args.Contains("--version") &&
                    (args.Contains("-h") ||
                    args.Contains("--help") ||
                    args.Contains("--dry-run")))
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
