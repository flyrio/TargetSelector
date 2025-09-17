using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using ECommons.GameFunctions;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using Lumina.Excel.Sheets;
using SamplePlugin.Utils;
using Status = Lumina.Excel.Sheets.Status;

namespace TargetSelector.Windows;

/// <summary>
/// 核心功能类，提供各种游戏相关的辅助方法（优化版）
/// </summary>
public class Core
{
    private readonly IClientState clientState;

    /// <summary> 技能类型枚举 </summary>
    public enum SpellType { None, RealGcd, GeneralGcd, Ability }

    public Core(IClientState clientState) { this.clientState = clientState; }

    /// <summary> 当前玩家 </summary>
    public IBattleChara? Me => Svc.ClientState.LocalPlayer;

    /// <summary> 获取对象的当前目标 </summary>
    public IGameObject? CurrTarget(IGameObject obj)
    {
        if (obj == null) return null;
        return Svc.Objects.SearchById(obj.TargetObjectId);
    }

    /// <summary> 设置目标 </summary>
    public void SetTarget(IBattleChara tar)
    {
        if (tar != null && tar.IsTargetable)
            Svc.Targets.Target = tar;
    }

    /// <summary> 设置焦点目标 </summary>
    public void FocusTarget(IBattleChara tar)
    {
        if (tar != null && tar.IsTargetable)
            Svc.Targets.FocusTarget = tar;
    }

    /// <summary> 检查身上有没有 buff </summary>
    public bool HasAura(IBattleChara battleCharacter, uint id, int timeLeft = 0)
    {
        if (battleCharacter == null) return false;

        var list = battleCharacter.StatusList;
        for (int i = 0; i < list.Length; i++)
        {
            var s = list[i];
            if (s == null || s.StatusId == 0) continue;
            if (s.StatusId == id)
            {
                if (timeLeft == 0 || s.RemainingTime * 1000f >= timeLeft)
                    return true;
            }
        }
        return false;
    }

    /// <summary> 是否处于 PvP </summary>
    public bool IsPvP() => Svc.ClientState.IsPvP;

    /// <summary> 获取范围内的所有角色 </summary>
    public List<IBattleChara> 获取附近所有目标(float range = 50)
    {
        var list = new List<IBattleChara>();
        var me = Svc.ClientState.LocalPlayer;
        if (me == null) return list;

        float r2 = range * range;
        foreach (var o in Svc.Objects)
        {
            if (o is IBattleChara bc)
            {
                if (Vector3.DistanceSquared(me.Position, bc.Position) <= r2)
                    list.Add(bc);
            }
        }
        return list;
    }

    /// <summary> 是否敌对 </summary>
    public unsafe bool IsEnemy(IGameObject obj)
    {
        if (obj == null) return false;
        return ActionManager.CanUseActionOnTarget(7u, obj.Struct());
    }

    /// <summary> 和玩家的距离 </summary>
    public float DistanceToPlayer(IGameObject? obj)
    {
        if (obj == null) return float.MaxValue;
        var me = Player.Object;
        return me == null ? float.MaxValue : Vector3.Distance(me.Position, obj.Position) - obj.HitboxRadius;
    }

    /// <summary> 和玩家的距离（保留 1 位小数，无 GC） </summary>
    public float DistanceToPlayerOne(IGameObject? obj)
    {
        if (obj == null) return float.MaxValue;
        var me = Player.Object;
        if (me == null) return float.MaxValue;

        float distance = Vector3.Distance(me.Position, obj.Position) - obj.HitboxRadius;
        return MathF.Round(distance, 1);
    }

    /// <summary> 是否可以驱散 </summary>
    private static readonly Lumina.Excel.ExcelSheet<Status>? StatusSheet =
        Svc.Data.GetExcelSheet<Status>();

    public bool HasCanDispel(IBattleChara bc)
    {
        if (bc == null) return false;
        foreach (var st in bc.StatusList)
        {
            if (st == null || st.StatusId == 0) continue;
            var row = StatusSheet?.GetRowOrDefault(st.StatusId);
            if (row.HasValue && row.Value.CanDispel)
                return true;
        }
        return false;
    }

    /// <summary> 获取附近敌人 </summary>
    public List<IBattleChara> GetNearbyEnemies(float range = 50)
    {
        var list = new List<IBattleChara>();
        var me = Svc.ClientState.LocalPlayer;
        if (me == null) return list;

        float r2 = range * range;
        foreach (var o in Svc.Objects)
        {
            if (o is IBattleChara bc && bc.IsValid())
            {
                if (Vector3.DistanceSquared(me.Position, bc.Position) <= r2)
                    list.Add(bc);
            }
        }
        return list;
    }

    /// <summary> 范围内优先级最高的标记目标 </summary>
    public static IBattleChara? GetHighestPriorityMarkedTargetInRange(Plugin plugin, float range, Core core)
    {
        var me = Svc.ClientState.LocalPlayer;
        if (me == null) return null;

        var marker = plugin.Configuration.SelectedHeadMarker;
        if (plugin.Configuration.MarkerEnabled.TryGetValue(marker, out bool enabled) && enabled)
        {
            var target = MarkerHelper.GetCharacterByHeadMarker(marker, core);
            if (target != null && target.IsValid())
            {
                if (Vector3.DistanceSquared(me.Position, target.Position) <= range * range)
                    return target;
            }
        }
        return null;
    }

    /// <summary> 检查是否在技能范围内 </summary>
    public static unsafe bool IsTargetVisibleOrInRange(uint actionId, IBattleChara? target)
    {
        var me = Svc.ClientState.LocalPlayer;
        if (me != null && target != null && target.IsTargetable)
        {
            var skillTarget = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)target.Address;
            var mePtr = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)me.Address;

            if (ActionManager.GetActionInRangeOrLoS == null) return false;
            if (!LineOfSightChecker.IsBlocked(mePtr, skillTarget))
                return ActionManager.GetActionInRangeOrLoS(actionId, mePtr, skillTarget) is not (566 or 562);
        }
        return false;
    }

    // ---- 小队 ----
    public static class Party
    {
        public static unsafe List<IBattleChara> PartyMembers(CombatRole role, float range = 50)
        {
            var list = new List<IBattleChara>();
            var me = Svc.ClientState.LocalPlayer;
            if (me == null) return list;

            float r2 = range * range;
            foreach (var member in GetPartyMembers())
            {
                if (member == null) continue;
                if (role != CombatRole.NonCombat && member is ICharacter ch && ch.GetRole() != role) continue;
                if (range != float.MaxValue && Vector3.DistanceSquared(me.Position, member.Position) > r2) continue;
                list.Add(member);
            }
            return list;
        }

        private static unsafe List<IBattleChara> GetPartyMembers()
        {
            var list = new List<IBattleChara>();
            var pronoun = Framework.Instance()->GetUIModule()->GetPronounModule();
            for (int i = 1; i <= Svc.Party.Length; i++)
            {
                var ptr = pronoun->ResolvePlaceholder($"<{i}>", 0, 0);
                if (ptr != null && ptr->EntityId != 0)
                {
                    var o = Svc.Objects.SearchById(ptr->EntityId);
                    if (o is IBattleChara bc)
                        list.Add(bc);
                }
            }
            return list;
        }
    }

    public static class PartyHelper
    {
        public static List<IBattleChara> GetAllPartyMembersByRole(float range = 50)
        {
            var list = new List<IBattleChara>();
            list.AddRange(Party.PartyMembers(CombatRole.Tank, range));
            list.AddRange(Party.PartyMembers(CombatRole.Healer, range));
            list.AddRange(Party.PartyMembers(CombatRole.DPS, range));
            return list;
        }
    }
}
