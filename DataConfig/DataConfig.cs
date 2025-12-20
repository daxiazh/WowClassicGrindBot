using System;
using Newtonsoft.Json;

using System.IO;
using System.Reflection;

// 添加日志相关的命名空间
using System.Diagnostics;

using static Newtonsoft.Json.JsonConvert;
using static System.IO.File;
using static System.IO.Path;

public static class DataConfigMeta
{
    public const int Version = 14;
    public const string DefaultFileName = "data_config.json";
}

public sealed class DataConfig
{
    public int Version = DataConfigMeta.Version;
    public string Root { get; } = GetDefaultRootPath();

    [JsonIgnore]
    public string Class => Join(Root, "class");
    [JsonIgnore]
    public string Path => Join(Root, "path");
    [JsonIgnore]
    public string ExpDbc => Join(Root, "dbc", Exp);
    [JsonIgnore]
    public string PathInfo => Join(Root, "PathInfo");
    [JsonIgnore]
    public string MPQ => Join(Root, "MPQ");
    [JsonIgnore]
    public string ExpArea => Join(Root, "area", Exp);
    [JsonIgnore]
    public string PPather => Join(Root, "PPather");
    [JsonIgnore]
    public string Screenshot => Join(Root, "cap");
    [JsonIgnore]
    public string ExpHistory => Join(Root, "History", Exp);
    [JsonIgnore]
    public string ExpExperience => Join(Root, "experience", Exp);
    [JsonIgnore]
    public string Leaflet => Join(Root, "leaflet", Exp);
    [JsonIgnore]
    public string Subzones => Join(Root, "subzones", Exp);

    [JsonIgnore]
    public string NpcSpawnLocations => Join(Root, "npcspawnlocations", Exp);

    // at runtime - determined from the running exe file version
    [JsonIgnore]
    public string Exp { get; set; } = "wrath"; // hardcoded default

    public static DataConfig Load()
    {
        if (File.Exists(DataConfigMeta.DefaultFileName))
        {
            var loaded = DeserializeObject<DataConfig>(ReadAllText(DataConfigMeta.DefaultFileName));
            if (loaded.Version == DataConfigMeta.Version)
                return loaded;
        }

        return new DataConfig().Save();
    }

    public static DataConfig Load(string client)
    {
        if (File.Exists(DataConfigMeta.DefaultFileName))
        {
            var loaded = DeserializeObject<DataConfig>(ReadAllText(DataConfigMeta.DefaultFileName));
            if (loaded.Version == DataConfigMeta.Version)
            {
                loaded.Exp = client.ToLowerInvariant();
                return loaded;
            }
        }

        DataConfig newConfig = new DataConfig().Save();
        newConfig.Exp = client.ToLowerInvariant();
        return newConfig;
    }

    private DataConfig Save()
    {
        WriteAllText(DataConfigMeta.DefaultFileName, SerializeObject(this));

        return this;
    }

    public void DeletePPatherCache()
    {
        if (!Directory.Exists(PathInfo))
        {
            return;
        }

        var directories = Directory.GetDirectories(PathInfo);
        foreach (string directory in directories)
        {
            Directory.Delete(directory, true);
        }
    }

    private static string GetDefaultRootPath()
    {
        // 2. 检查当前工作目录
        string currentDirJsonPath = Join(Environment.CurrentDirectory, "json");
        Console.WriteLine($"[DataConfig] 检查当前工作目录下的json路径: {currentDirJsonPath}");
        
        if (Directory.Exists(currentDirJsonPath))
        {
            Console.WriteLine($"[DataConfig] ✅ 找到数据目录: {currentDirJsonPath}");
            // 确保返回绝对路径
            return GetFullPath(currentDirJsonPath);
        }
        else
        {
            Console.WriteLine($"[DataConfig] ❌ 未找到目录: {currentDirJsonPath}");
        }
        
        // 3. 检查当前工作目录的父目录
        string parentDirJsonPath = Join(GetDirectoryName(Environment.CurrentDirectory), "json");
        Console.WriteLine($"[DataConfig] 检查当前工作目录上级目录下的json路径: {parentDirJsonPath}");
        
        if (Directory.Exists(parentDirJsonPath))
        {
            Console.WriteLine($"[DataConfig] ✅ 找到数据目录: {parentDirJsonPath}");
            // 确保返回绝对路径
            return GetFullPath(parentDirJsonPath);
        }
        else
        {
            Console.WriteLine($"[DataConfig] ❌ 未找到目录: {parentDirJsonPath}");
        }
        
        // 4. 回退到默认相对路径
        string fallbackPath = Join("..", "json");
        Console.WriteLine($"[DataConfig] 使用回退路径: {fallbackPath}");
        Console.WriteLine($"[DataConfig] ⚠️  警告: 使用回退路径可能无法找到数据文件");
        // 即使是回退路径也要确保返回绝对路径
        // 使用 Combine 确保得到绝对路径
        return GetFullPath(Combine(Environment.CurrentDirectory, fallbackPath));
    }
}