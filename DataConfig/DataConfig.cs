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

    /// <summary>
    /// 向上搜索 json/ 目录 (从指定目录开始)
    /// </summary>
    /// <param name="startDir">起始目录</param>
    /// <param name="maxLevels">最大向上搜索层数</param>
    /// <returns>找到的 json/ 目录绝对路径,未找到返回 null</returns>
    private static string? SearchUpwardsForJson(string? startDir, int maxLevels)
    {
        if (string.IsNullOrEmpty(startDir))
            return null;

        string? current = startDir;
        
        for (int i = 0; i < maxLevels; i++)
        {
            string jsonPath = Join(current, "json");
            
            if (Directory.Exists(jsonPath))
            {
                Console.WriteLine($"[DataConfig] ✅ 找到数据目录 (向上搜索 {i} 层): {jsonPath}");
                return GetFullPath(jsonPath);
            }
            
            string? parent = GetDirectoryName(current);
            
            // 已到达根目录
            if (parent == null || parent == current)
                break;
            
            current = parent;
        }
        
        return null;
    }

    /// <summary>
    /// 获取默认的数据根路径 (Json/ 目录)
    /// 兼容 Rider 调试、dotnet run 和打包后运行
    /// </summary>
    /// <returns>Json/ 目录的绝对路径</returns>
    private static string GetDefaultRootPath()
    {
        Console.WriteLine("[DataConfig] 开始查找 Json/ 数据目录...");
        
        // 1. 从程序集位置向上查找 (最可靠 - 兼容调试和打包运行)
        string? assemblyDir = GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        Console.WriteLine($"[DataConfig] 程序集位置: {assemblyDir}");
        
        string? jsonPath = SearchUpwardsForJson(assemblyDir, maxLevels: 6);
        if (jsonPath != null)
            return jsonPath;
        
        Console.WriteLine("[DataConfig] ❌ 从程序集位置未找到 json/ 目录");
        
        // 2. 检查当前工作目录
        string currentDirJsonPath = Join(Environment.CurrentDirectory, "json");
        Console.WriteLine($"[DataConfig] 检查当前工作目录: {currentDirJsonPath}");
        
        if (Directory.Exists(currentDirJsonPath))
        {
            Console.WriteLine($"[DataConfig] ✅ 找到数据目录 (当前工作目录): {currentDirJsonPath}");
            return GetFullPath(currentDirJsonPath);
        }
        
        Console.WriteLine("[DataConfig] ❌ 当前工作目录未找到 json/ 目录");
        
        // 3. 检查当前工作目录的父目录
        string? parentDirJsonPath = Join(GetDirectoryName(Environment.CurrentDirectory), "json");
        Console.WriteLine($"[DataConfig] 检查父目录: {parentDirJsonPath}");
        
        if (parentDirJsonPath != null && Directory.Exists(parentDirJsonPath))
        {
            Console.WriteLine($"[DataConfig] ✅ 找到数据目录 (父目录): {parentDirJsonPath}");
            return GetFullPath(parentDirJsonPath);
        }
        
        Console.WriteLine("[DataConfig] ❌ 父目录未找到 json/ 目录");
        
        // 4. 回退到默认相对路径
        string fallbackPath = Combine(Environment.CurrentDirectory, "..", "json");
        Console.WriteLine($"[DataConfig] ⚠️  使用回退路径: {fallbackPath}");
        Console.WriteLine($"[DataConfig] ⚠️  警告: 回退路径可能无法找到数据文件,请检查 Json/ 目录位置");
        
        return GetFullPath(fallbackPath);
    }
}