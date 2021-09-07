// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Kube.Apps
{
    public sealed class Config
    {
        private static readonly JsonSerializerOptions JsonOptions = new ()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            IgnoreNullValues = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        };

        public bool DryRun { get; set; }

        public static Dictionary<string, object> ReadKapConfig()
        {
            DateTime now = DateTime.UtcNow;

            Dictionary<string, object> cfg = new ();

            if (File.Exists(Dirs.ConfigFile))
            {
                cfg = JsonSerializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(Dirs.ConfigFile), JsonOptions);
            }
            else
            {
                IEnumerable<string> files = Directory.EnumerateFiles(".", "*.csproj");

                if (files.Any())
                {
                    // TODO - read csproj file for assembly name

                    cfg["name"] = Path.GetFileNameWithoutExtension(files.First<string>());
                }
                else
                {
                    cfg["name"] = Path.GetFileName(Directory.GetCurrentDirectory());
                    cfg["imageName"] = $"k3d-registry.localhost:5000/{cfg["name"]}";
                    cfg["imageTag"] = "local";
                }
            }

            if (!cfg.ContainsKey("name"))
            {
                cfg["name"] = "app";
            }

            if (!cfg.ContainsKey("imageName"))
            {
                cfg["imageName"] = $"k3d-registry.localhost:5000/{cfg["name"]}";
            }

            if (!cfg.ContainsKey("imageTag"))
            {
                cfg["imageTag"] = "local";
            }

            if (!cfg.ContainsKey("namespace"))
            {
                cfg["namespace"] = "default";
            }

            if (!cfg.ContainsKey("port"))
            {
                cfg["port"] = 8080;
            }

            if (!cfg.ContainsKey("nodePort"))
            {
                cfg["nodePort"] = 30080;
            }

            if (!cfg.ContainsKey("version"))
            {
                cfg["version"] = now.ToString("MMdd-HHmm");
            }

            if (!cfg.ContainsKey("deploy"))
            {
                cfg["deploy"] = now.ToString("yyyy-MM-dd-HH-mm-ss");
            }

            return cfg;
        }
    }
}
