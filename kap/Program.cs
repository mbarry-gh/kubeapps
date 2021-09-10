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
                ShellExec.Run(ShellExec.Git, $"clone https://github.com/bartr/kap-config {Dirs.KapHome}");
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
