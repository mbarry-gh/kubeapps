// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.IO;

namespace Kube.Apps
{
    public class Dirs
    {
        public const string HomeSubDir = ".kubeapps";
        public const string TemplateFile = "kubeapps/template.yaml";
        public const string ConfigFile = "kubeapps/config.json";
        public const string DotnetConfig = "config.json";
        public const string DotnetTemplate = "template.yaml";

        public static bool IsWindows { get; set; }

        public static bool IsLinux => !IsWindows;

        public static string KapBase { get; set; }
        public static string KapHome { get; set; }
        public static string KapStart { get; set; }
        public static string GitOpsBase { get; set; } = "/workspaces/gitops";
        public static string GitOpsDir { get; set; } = "/workspaces/gitops/gitops";
        public static string GitOpsBootstrapDir { get; set; } = "/workspaces/gitops/gitops/bootstrap";

        public static string KapBootstrapDir => Path.Combine(KapHome, "bootstrap");

        public static string KapDotnetDir => Path.Combine(KapHome, "dotnet");

        public static bool IsAppDir
        {
            get
            {
                return Directory.Exists("./kubeapps") && File.Exists("./kubeapps/config.json");
            }
        }
    }
}
