using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using System;
using System.Linq;
using ECommons.DalamudServices;
using ECommons.Logging;
using Lumina.Excel.GeneratedSheets;
using TargetSelector.Windows;

public static class MarkerHelper
{
    // 定义头标枚举，确保与游戏中的头标索引对应 (0-13)
    public enum HeadMarker : int
    {
        攻击1,
        攻击2,
        攻击3,
        攻击4,
        攻击5,
        锁链1,
        锁链2,
        锁链3,
        禁止1,
        禁止2,
        方块,
        圆圈,
        十字,
        三角
    }

    // 获取指定头标的目标ID
    private static unsafe GameObjectId GetHeadMarkerTargetId(HeadMarker marker)
    {
        int markerIndex = (int)marker;
        //PluginLog.Debug($"Marker: {marker}, Marker Index: {markerIndex}"); // Log the marker and its index
        if (markerIndex >= 0 && markerIndex < 14)                          // 检查索引有效性 (头标索引 0-13)
        {

            var targetId = MarkingController.Instance()->Markers[markerIndex];
            //PluginLog.Debug($"Target ID for marker {marker}: {targetId.ObjectId}"); // Log the target ID
            return targetId;
            
        } ;
        //PluginLog.Debug($"Invalid marker index: {markerIndex}");
        return default;
    }


    // 根据目标ID获取头标
    public static HeadMarker? GetHeadMarkerByObjectId(uint objectId)
    {
        unsafe
        {
            for (int i = 0; i < 14; i++) // 只检查头标索引 (0-13)
            {
                if (objectId == MarkingController.Instance()->Markers[i].ObjectId)
                {
                    // 将索引转换为 HeadMarker 枚举
                    if (Enum.IsDefined(typeof(HeadMarker), i))
                    {
                        return (HeadMarker)i;
                    }
                    else
                    {
                        // 处理未知的头标索引 (虽然理论上不应该出现)
                        return null;
                    }
                }
            }
        }
        return null;
    }

    // 根据头标获取角色
    public static IBattleChara? GetCharacterByHeadMarker(HeadMarker marker, Core core) // 添加 core 参数
    {
        try
        {
            GameObjectId markerTargetId = GetHeadMarkerTargetId(marker);
            if (markerTargetId.ObjectId == 0)
            {
                return null;
            }


            //  优先在附近目标中查找，使用 Core 实例
            return core.获取附近所有目标().FirstOrDefault(character => character.GameObjectId == markerTargetId.ObjectId)
                   ?? Svc.Objects.Where(obj => obj is IBattleChara battleChara && obj.GameObjectId == markerTargetId.ObjectId).Cast<IBattleChara>().FirstOrDefault(); // 使用 LINQ 简化
        }
        catch (Exception ex)
        {
            //PluginLog.LogError($"Error in GetCharacterByHeadMarker: {marker}");
            return null;
        }
    }
}
