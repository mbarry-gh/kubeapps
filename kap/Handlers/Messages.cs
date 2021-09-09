// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Kube.Apps
{
    /// <summary>
    /// Output messages
    /// </summary>
    public class Messages
    {
        public const string UnableCreateDir = "unable to create GitOps directory";
        public const string HandlerNotFound = "command line handler not found\n\n  Please report this as a bug";
        public const string SourceDirNotFound = "Source directory does not exist or could not be found";

        public static readonly string GitOpsBaseNotFound = $"{Dirs.GitOpsBase} not found\n\n  Please clone a git repo to {Dirs.GitOpsBase} and try again";
        public static readonly string ConfigFileNotFound = $"Could not find {Dirs.ConfigFile}";
        public static readonly string GitOpsDirNotFound = $"{Dirs.GitOpsDir} is missing";
    }
}
