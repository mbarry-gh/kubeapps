// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.IO;

namespace Kube.Apps
{
    /// <summary>
    /// Execute shell commands
    /// </summary>
    public sealed class ShellExec
    {
        public static bool Run(string cmd, string cmdParams = "")
        {
            if (string.IsNullOrWhiteSpace(cmd))
            {
                return false;
            }

            cmdParams = string.IsNullOrWhiteSpace(cmdParams) ? string.Empty : cmdParams.Trim();

            try
            {
                using System.Diagnostics.Process proc = new ();
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.RedirectStandardOutput = false;

                proc.StartInfo.FileName = cmd;

                if (!string.IsNullOrWhiteSpace(cmdParams))
                {
                    proc.StartInfo.Arguments = cmdParams.Trim();
                }

                proc.Start();
                proc.WaitForExit();

                return proc.ExitCode == 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ShellExec exception: {cmd} {cmdParams} {ex.Message}");
                return false;
            }
        }

        // check git repo for changes
        public static bool HasGitChanges()
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
                Console.WriteLine($"HasGitChanges exception: git {command}\n{ex.Message}");
                return false;
            }
        }
    }
}
