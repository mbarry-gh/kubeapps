// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Kube.Apps
{
    /// <summary>
    /// Execute shell commands
    /// </summary>
    public sealed partial class App
    {
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
    }
}
