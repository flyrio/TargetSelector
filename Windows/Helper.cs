using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using ECommons.GameFunctions;
using ECommons.GameHelpers;
using ECommons.Logging;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using Lumina.Excel.GeneratedSheets2;
using GameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace TargetSelector.Windows;

public class Core
{
    
    private readonly IClientState clientState; // 添加 clientState 字段

    public Core(IClientState clientState) // 添加构造函数
    {
        this.clientState = clientState;
    }


    public IBattleChara? Me => Svc.ClientState.LocalPlayer; // 使用 clientState
    public IGameObject? CurrTarget(IGameObject battleChara) => battleChara == null ? null : Svc.Objects.SearchById(battleChara.TargetObjectId);
    public void SetTarget(IGameObject? tar)
    {
        if (tar == null || !tar.IsTargetable) return;
        Svc.Targets.Target = tar;
    }
    
    public bool HasAura(IBattleChara battleCharacter, uint id, int timeLeft = 0)
    {
        if (battleCharacter == null)
            return false;
        for (int index = 0; index < battleCharacter.StatusList.Length; ++index)
        {
            Dalamud.Game.ClientState.Statuses.Status status = battleCharacter.StatusList[index];
            if (status != null && status.StatusId != 0U && (int) status.StatusId == (int) id)
            {
                DefaultInterpolatedStringHandler interpolatedStringHandler = new DefaultInterpolatedStringHandler(36, 5);
                interpolatedStringHandler.AppendLiteral("CheckHasAura  ");
                interpolatedStringHandler.AppendFormatted<uint>(id);
                interpolatedStringHandler.AppendLiteral(" ==> ");
                interpolatedStringHandler.AppendFormatted<uint>(status.StatusId);
                interpolatedStringHandler.AppendLiteral(" ");
                interpolatedStringHandler.AppendFormatted<Lumina.Text.SeString>(status.GameData.Name);
                interpolatedStringHandler.AppendLiteral("  Remain ");
                interpolatedStringHandler.AppendFormatted<float>(status.RemainingTime);
                interpolatedStringHandler.AppendLiteral(" Param ");
                interpolatedStringHandler.AppendFormatted<ushort>(status.Param);
                if (timeLeft == 0 || (double) Math.Abs(status.RemainingTime) * 1000.0 >= (double) timeLeft)
                    return true;
            }
        }
        return false;
    }
    
    public bool IsPvP()
    {
        return Svc.ClientState.IsPvP;
    }
    
    public List<IBattleChara> 获取附近所有目标(float range = 50)
    {
        var playerPos = Svc.ClientState.LocalPlayer?.Position;
        if (playerPos == null) return new List<IBattleChara>();

        return Svc.Objects.OfType<IBattleChara>()
                  .Where(bc => Vector3.DistanceSquared(bc.Position, playerPos.Value) <= range * range)
                  .ToList();
    }
    
    public unsafe bool IsEnemy(IGameObject obj)
    {
        return obj != null && ActionManager.CanUseActionOnTarget(7U, obj.Struct());
    }
    
    public float DistanceToPlayer(IGameObject? obj)
    {
        if (obj == null)
            return float.MaxValue;
        IPlayerCharacter playerCharacter = Player.Object;
        return playerCharacter == null ? float.MaxValue : Vector3.Distance(playerCharacter.Position, obj.Position) - obj.HitboxRadius;
    }
    
    public static class Party
    {
        public static unsafe List<IBattleChara> PartyMembers(CombatRole role, float range = 50)
        {
            var partyMembers = GetPartyMembers(); // 获取小队成员

            var playerPos = Svc.ClientState.LocalPlayer?.Position;
            if (playerPos == null) return new List<IBattleChara>();

            // 筛选
            return partyMembers.Where(member =>
            {
                if (member == null) return false;

                if (role != CombatRole.NonCombat && member is ICharacter character && character.GetRole() != role)
                {
                    return false;
                }

                if (range != float.MaxValue && Vector3.DistanceSquared(member.Position, playerPos.Value) > range * range)
                {
                    return false;
                }

                return true;
            }).ToList();
        }


        private static unsafe List<IBattleChara> GetPartyMembers()
        {
            var party = new List<IBattleChara>();
            for (int i = 1; i <= Svc.Party.Length; ++i)
            {
                var pronounModule = Framework.Instance()->GetUIModule()->GetPronounModule();
                string placeholder = $"<{i}>"; // 简化字符串构建

                var gameObjectPtr = pronounModule->ResolvePlaceholder(placeholder, 0, 0);
                if (gameObjectPtr != null && gameObjectPtr->EntityId != 0)
                {
                    var obj = Svc.Objects.SearchById(gameObjectPtr->EntityId);
                    party.Add(obj as IBattleChara);
                }
            }
            return party;
        }
    }
    
    public static class PartyHelper //或者其他合适的类名
    {
        public static List<IBattleChara> GetAllPartyMembersByRole(float range = 50)
        {
            var tanks = Party.PartyMembers(CombatRole.Tank, range);
            var healers = Party.PartyMembers(CombatRole.Healer, range);
            var dps = Party.PartyMembers(CombatRole.DPS, range);

            // 合并列表
            var allMembers = new List<IBattleChara>();
            allMembers.AddRange(tanks);
            allMembers.AddRange(healers);
            allMembers.AddRange(dps);

            return allMembers;


            // 或者，使用 LINQ Concat 方法 (更简洁)
            //return tanks.Concat(healers).Concat(dps).ToList();
        }
    }
    
    public bool HasCanDispel(IBattleChara battleCharacter)
    {
        if (battleCharacter == null)
            return false;
        foreach (Dalamud.Game.ClientState.Statuses.Status status in battleCharacter.StatusList)
        {
            if (Svc.Data.GetExcelSheet<Lumina.Excel.GeneratedSheets.Status>().GetRow(status.StatusId).CanDispel)
                return true;
        }
        return false;
    }
    
    public List<IBattleChara> GetNearbyEnemies(float range = 50)
    {
        var playerPos = Svc.ClientState.LocalPlayer?.Position;
        if (playerPos == null)
        {
            return new List<IBattleChara>();
        }

        return Svc.Objects.OfType<IBattleChara>()
                  .Where(enemy =>
                  {
                      if (enemy == null || !enemy.IsValid()) return false; // 检查是否为有效目标 (类似 ValidAttackUnit)

                      // 检查距离，使用平方距离优化性能
                      return Vector3.DistanceSquared(enemy.Position, playerPos.Value) <= range * range;
                  })
                  .ToList();
    }

    public static IBattleChara? GetHighestPriorityMarkedTargetInRange(Plugin plugin, float range, Core core)
    {
        var playerPos = Svc.ClientState.LocalPlayer?.Position;
        if (playerPos == null)
        {
            //PluginLog.LogDebug("Player position is null.");
            return null;
        }

        var selectedMarker = plugin.Configuration.SelectedHeadMarker;
        //PluginLog.LogDebug($"Selected Head Marker in GetHighestPriorityMarkedTargetInRange: {selectedMarker}");

        // Only check the selected marker
        if (plugin.Configuration.MarkerEnabled.TryGetValue(selectedMarker, out var enabled) && enabled)
        {
            //PluginLog.LogDebug($"Checking selected marker: {selectedMarker}");
            var target = MarkerHelper.GetCharacterByHeadMarker(selectedMarker, core);
            if (target != null && target.IsValid() && Vector3.DistanceSquared(target.Position, playerPos.Value) <= range * range)
            {
                //PluginLog.LogDebug($"Returning selected marker target: {target.Name}");
                return target;
            }
            else
            {
                //PluginLog.LogDebug($"No target found with selected marker {selectedMarker} within range.");
                return null; // Return null if no target with the selected marker is found
            }
        }
        else
        {
            //PluginLog.LogDebug($"Selected marker {selectedMarker} is disabled.");
            return null; // Return null if the selected marker is disabled
        }
    }
    
    public static bool IsTargetVisibleOrInRange(uint actionId, IBattleChara? target) //目标在技能攻击范围内且可被技能命中
    {
        unsafe
        {
            if (Svc.ClientState.LocalPlayer != null && target != null && target.IsTargetable)
            { 
                var skilltarget = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)target.Address;
                var me = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)Svc.ClientState.LocalPlayer.Address;
                if (ActionManager.GetActionInRangeOrLoS == null)
                {
                    return false;
                }

                return ActionManager.GetActionInRangeOrLoS(actionId, me, skilltarget) is not (566 or 562);
            }
        }

        return false;
    }
    
}
