using System;
using System.IO;
using System.Reflection;
using BepInEx.Logging;

namespace InputMonitorMod
{
    public class PluginConfig
    {
        public int Port { get; set; } = 8000;
        public int FrameSkip { get; set; } = 2;
        public float LeverReleaseTime { get; set; } = 0.15f;

        private static string GetConfigPath()
        {
            string pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            return Path.Combine(pluginDir, "config.ini");
        }

        public static PluginConfig Load(ManualLogSource logger)
        {
            var config = new PluginConfig();
            string configPath = GetConfigPath();

            if (!File.Exists(configPath))
            {
                logger.LogWarning($"Config file not found at {configPath}, using defaults");
                config.Save(logger);
                return config;
            }

            try
            {
                string[] lines = File.ReadAllLines(configPath);
                foreach (string line in lines)
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#") || trimmed.StartsWith(";"))
                        continue;

                    if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                        continue;

                    string[] parts = trimmed.Split('=');
                    if (parts.Length != 2)
                        continue;

                    string key = parts[0].Trim();
                    string value = parts[1].Trim();

                    switch (key.ToLower())
                    {
                        case "port":
                            if (int.TryParse(value, out int port))
                                config.Port = port;
                            break;
                        case "frameskip":
                            if (int.TryParse(value, out int frameSkip))
                                config.FrameSkip = Math.Max(1, frameSkip);
                            break;
                        case "leverreleasetime":
                            if (float.TryParse(value, out float releaseTime))
                                config.LeverReleaseTime = Math.Max(0.01f, releaseTime);
                            break;
                    }
                }

                logger.LogInfo($"Config loaded: Port={config.Port}, FrameSkip={config.FrameSkip}, LeverReleaseTime={config.LeverReleaseTime}s");
            }
            catch (Exception ex)
            {
                logger.LogError($"Failed to load config: {ex.Message}");
            }

            return config;
        }

        public void Save(ManualLogSource logger)
        {
            string configPath = GetConfigPath();
            try
            {
                string content = @"# 配置文件说明

[Network]
# WebSocket 服务器端口号 (默认: 8000)
Port = " + Port + @"

[Performance]
# 帧数跳过设置，每 N 帧检查一次状态 (默认: 2，即每2帧检查一次)
# 建议范围: 1-4（基于60hz刷新率计算，1等效于60fps，4等效于15fps）
FrameSkip = " + FrameSkip + @"

[Lever]
# 摇杆松手判定时间（秒）(默认: 0.15)
LeverReleaseTime = " + LeverReleaseTime.ToString("F2") + @"
# 摇杆静止超过此时间后判定为松手
";

                File.WriteAllText(configPath, content);
                logger.LogInfo($"Config file created at {configPath}");
            }
            catch (Exception ex)
            {
                logger.LogError($"Failed to save config: {ex.Message}");
            }
        }
    }
}
