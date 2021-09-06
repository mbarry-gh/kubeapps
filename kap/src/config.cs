// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Kube.Apps
{
    public class Config
    {
        public string Init { get; set; }
        public string Add { get; set; }
        public string Deploy { get; set; }
        public string AgoUser { get; set; }
        public string AgoPat { get; set; }
        public string AgoRepo { get; set; }
        public string ContainerVersion { get; set; }
        public bool LocalDev { get; set; }
        public string TemplateDir { get; set; }
        public string OutputDir { get; set; }
        public bool DryRun { get; set; }
    }
}
