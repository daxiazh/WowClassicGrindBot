using Newtonsoft.Json;

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace SharedLib;

public sealed class WorldMapAreaDB
{
    private readonly FrozenDictionary<int, WorldMapArea> wmas;

    public const int AreaIDOffset = 1000000;

    public IEnumerable<WorldMapArea> Values => wmas.Values;

    public FrozenDictionary<int, WorldMapArea> AreaHitbox;

    public WorldMapAreaDB(DataConfig dataConfig)
    {
        // 使用 Combine 而不是 Join 来确保获得正确的绝对路径
        string worldMapAreaPath = Path.Combine(dataConfig.ExpDbc, "WorldMapArea.json");
        Console.WriteLine($"[WorldMapAreaDB] 尝试加载文件: {worldMapAreaPath}");
        Console.WriteLine($"[WorldMapAreaDB] ExpDbc 路径: {dataConfig.ExpDbc}");
        Console.WriteLine($"[WorldMapAreaDB] Root 路径: {dataConfig.Root}");
        
        if (!File.Exists(worldMapAreaPath))
        {
            Console.WriteLine($"[WorldMapAreaDB] ❌ 文件不存在: {worldMapAreaPath}");
            // 如果文件不存在，尝试使用项目根目录下的路径
            string fallbackPath = Path.Combine(dataConfig.Root, "dbc", dataConfig.Exp, "WorldMapArea.json");
            Console.WriteLine($"[WorldMapAreaDB] 尝试回退路径: {fallbackPath}");
            
            if (File.Exists(fallbackPath))
            {
                worldMapAreaPath = fallbackPath;
                Console.WriteLine($"[WorldMapAreaDB] ✅ 使用回退路径: {worldMapAreaPath}");
            }
            else
            {
                Console.WriteLine($"[WorldMapAreaDB] ❌ 回退路径也不存在: {fallbackPath}");
                // 列出目录内容帮助调试
                string directory = Path.GetDirectoryName(worldMapAreaPath);
                if (Directory.Exists(directory))
                {
                    Console.WriteLine($"[WorldMapAreaDB] 目录 {directory} 中的文件:");
                    foreach (string file in Directory.GetFiles(directory))
                    {
                        Console.WriteLine($"[WorldMapAreaDB]   - {Path.GetFileName(file)}");
                    }
                }
                else
                {
                    Console.WriteLine($"[WorldMapAreaDB] 目录不存在: {directory}");
                }
                
                // 再次尝试列出回退路径的目录
                string fallbackDirectory = Path.GetDirectoryName(fallbackPath);
                if (Directory.Exists(fallbackDirectory))
                {
                    Console.WriteLine($"[WorldMapAreaDB] 回退目录 {fallbackDirectory} 中的文件:");
                    foreach (string file in Directory.GetFiles(fallbackDirectory))
                    {
                        Console.WriteLine($"[WorldMapAreaDB]   - {Path.GetFileName(file)}");
                    }
                }
                else
                {
                    Console.WriteLine($"[WorldMapAreaDB] 回退目录不存在: {fallbackDirectory}");
                }
                
                throw new FileNotFoundException($"无法找到 WorldMapArea.json 文件", worldMapAreaPath);
            }
        }
        else
        {
            Console.WriteLine($"[WorldMapAreaDB] ✅ 文件存在: {worldMapAreaPath}");
        }

        ReadOnlySpan<WorldMapArea> span =
            JsonConvert.DeserializeObject<WorldMapArea[]>(
                File.ReadAllText(worldMapAreaPath));

        Dictionary<int, WorldMapArea> areahitbox = [];
        Dictionary<int, WorldMapArea> wmas = [];
        for (int i = 0; i < span.Length; i++)
        {
            if (span[i].AreaID > AreaIDOffset)
            {
                areahitbox.TryAdd(span[i].AreaID, span[i]);
            }

            if (span[i].UIMapId == 0)
                continue;
            wmas.Add(span[i].UIMapId, span[i]);
        }

        this.wmas = wmas.ToFrozenDictionary();
        this.AreaHitbox = areahitbox.ToFrozenDictionary();
    }

    public int GetAreaId(int uiMap)
    {
        return wmas.TryGetValue(uiMap, out WorldMapArea map) ? map.AreaID : -1;
    }

    public int GetMapId(int uiMap)
    {
        return wmas.TryGetValue(uiMap, out WorldMapArea map) ? map.MapID : -1;
    }

    public bool TryGet(int uiMap, out WorldMapArea wma)
    {
        return wmas.TryGetValue(uiMap, out wma);
    }

    //

    public static Vector3 ToWorld_FlipXY(Vector3 map, in WorldMapArea wma)
    {
        return new Vector3(wma.ToWorldX(map.Y), wma.ToWorldY(map.X), map.Z);
    }

    public Vector3 ToWorld_FlipXY(int uiMap, Vector3 map)
    {
        return wmas.TryGetValue(uiMap, out WorldMapArea wma)
            ? new Vector3(wma.ToWorldX(map.Y), wma.ToWorldY(map.X), map.Z)
            : Vector3.Zero;
    }

    public void ToWorldXY_FlipXY(int uiMap, Vector3[] map)
    {
        WorldMapArea wma = wmas[uiMap];
        for (int i = 0; i < map.Length; i++)
        {
            Vector3 p = map[i];
            map[i] = new Vector3(wma.ToWorldX(p.Y), wma.ToWorldY(p.X), p.Z);
        }
    }

    //

    public Vector3 ToMap_FlipXY(Vector3 world, float mapId, int uiMap)
    {
        WorldMapArea wma = GetWorldMapArea(world.X, world.Y, (int)mapId, uiMap);
        return new Vector3(wma.ToMapY(world.Y), wma.ToMapX(world.X), world.Z);
    }

    public static Vector3 ToMap_FlipXY(Vector3 world, in WorldMapArea wma)
    {
        return new Vector3(wma.ToMapY(world.Y), wma.ToMapX(world.X), world.Z);
    }

    public void ToMap_FlipXY(int uiMap, Span<Vector3> worlds)
    {
        if (!TryGet(uiMap, out WorldMapArea wma))
            return;

        for (int i = 0; i < worlds.Length; i++)
        {
            Vector3 world = worlds[i];
            worlds[i] = new Vector3(wma.ToMapY(world.Y), wma.ToMapX(world.X), world.Z);
        }
    }

    //

    public WorldMapArea GetWorldMapArea(float worldX, float worldY, int mapId, int uiMap)
    {
        IEnumerable<WorldMapArea> maps =
            wmas.Values.Where(ContainsWorldPosAndMapId);

        bool ContainsWorldPosAndMapId(WorldMapArea i) =>
                worldX <= i.LocTop &&
                worldX >= i.LocBottom &&
                worldY <= i.LocLeft &&
                worldY >= i.LocRight &&
                i.MapID == mapId;

        if (!maps.Any())
        {
            throw new ArgumentOutOfRangeException(nameof(wmas), $"Failed to find map area for spot {worldX}, {worldY}, {mapId}");
        }

        if (maps.Count() > 1)
        {
            // sometimes we end up with 2 map areas which a coord could be in which is rather unhelpful. e.g. Silithus and Feralas overlap.
            // If we are in a zone and not moving between then the mapHint should take care of the issue
            // otherwise we are not going to be able to work out which zone we are actually in...

            if (uiMap > 0)
            {
                return maps.First(ByUIMapId);
                bool ByUIMapId(WorldMapArea m) => m.UIMapId == uiMap;
            }
            throw new ArgumentOutOfRangeException(nameof(wmas), $"Found many map areas for spot {worldX}, {worldY}, {mapId} : {string.Join(", ", maps.Select(s => s.AreaName))}");
        }

        return maps.First();
    }

    public WorldMapArea GetByAreaId(int areaId)
    {
        return wmas.Values.FirstOrDefault(x => x.AreaID == areaId);
    }

    public WorldMapArea GetByAreaIdHit(int areaIdHit)
    {
        return AreaHitbox.TryGetValue(areaIdHit + AreaIDOffset, out WorldMapArea wma)
            ? wma
            : wmas.Values.FirstOrDefault(x => x.AreaID == areaIdHit);
    }

}