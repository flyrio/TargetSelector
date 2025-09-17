using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using ECommons.GameFunctions;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using Lumina.Text;
using SamplePlugin.Utils;

namespace TargetSelector.Windows;

/// <summary>
/// 核心功能类，提供各种游戏相关的辅助方法
/// </summary>
public class Core
{
    private readonly IClientState clientState;

    /// <summary>
    /// 技能类型枚举
    /// </summary>
    public enum SpellType
    {
        None,
        RealGcd,
        GeneralGcd,
        Ability,
    }
    
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="clientState">客户端状态服务</param>
    public Core(IClientState clientState)
    {
        this.clientState = clientState;
    }

    /// <summary>
    /// 获取当前玩家
    /// </summary>
    public IBattleChara? Me => Svc.ClientState.LocalPlayer;
    
    /// <summary>
    /// 获取指定游戏对象的当前目标
    /// </summary>
    /// <param name="battleChara">要查询的游戏对象</param>
    /// <returns>目标对象，如果没有目标则返回null</returns>
    public IGameObject? CurrTarget(IGameObject battleChara) => 
        battleChara == null ? null : Svc.Objects.SearchById(battleChara.TargetObjectId);
    
    /// <summary>
    /// 设置当前目标
    /// </summary>
    /// <param name="tar">要设置为目标的战斗角色</param>
    public void SetTarget(IBattleChara tar)
    {
        if (tar == null || !tar.IsTargetable) return;
        Svc.Targets.Target = tar;
    }
    
    /// <summary>
    /// 设置焦点目标
    /// </summary>
    /// <param name="tar">要设置为焦点目标的战斗角色</param>
    public void FocusTarget(IBattleChara tar)
    {
        if (tar == null || !tar.IsTargetable) return;
        Svc.Targets.FocusTarget = tar;
    }
    
    /// <summary>
    /// 检查目标是否有指定的状态效果
    /// </summary>
    /// <param name="battleCharacter">战斗角色</param>
    /// <param name="id">状态效果ID</param>
    /// <param name="timeLeft">剩余时间阈值（毫秒）</param>
    /// <returns>是否有指定状态效果</returns>
    public bool HasAura(IBattleChara battleCharacter, uint id, int timeLeft = 0)
    {
        if (battleCharacter == null)
            return false;
            
        for (int index = 0; index < battleCharacter.StatusList.Length; ++index)
        {
            var status = battleCharacter.StatusList[index];
            if (status != null && status.StatusId != 0U && (int)status.StatusId == (int)id)
            {
                DefaultInterpolatedStringHandler interpolatedStringHandler = new DefaultInterpolatedStringHandler(36, 5);
                interpolatedStringHandler.AppendLiteral("CheckHasAura  ");
                interpolatedStringHandler.AppendFormatted<uint>(id);
                interpolatedStringHandler.AppendLiteral(" ==> ");
                interpolatedStringHandler.AppendFormatted<uint>(status.StatusId);
                interpolatedStringHandler.AppendLiteral(" ");
                interpolatedStringHandler.AppendFormatted<Lumina.Text.SeString>(new SeString(status.GameData.Value.Name));
                interpolatedStringHandler.AppendLiteral("  Remain ");
                interpolatedStringHandler.AppendFormatted<float>(status.RemainingTime);
                interpolatedStringHandler.AppendLiteral(" Param ");
                interpolatedStringHandler.AppendFormatted<ushort>(status.Param);
                
                if (timeLeft == 0 || (double)Math.Abs(status.RemainingTime) * 1000.0 >= (double)timeLeft)
                    return true;
            }
        }
        return false;
    }
    
    /// <summary>
    /// 检查当前是否处于PvP状态
    /// </summary>
    /// <returns>是否在PvP中</returns>
    public bool IsPvP()
    {
        return Svc.ClientState.IsPvP;
    }
    
    /// <summary>
    /// 获取指定范围内的所有目标
    /// </summary>
    /// <param name="range">范围，默认为50</param>
    /// <returns>范围内的战斗角色列表</returns>
    public List<IBattleChara> 获取附近所有目标(float range = 50)
    {
        var playerPos = Svc.ClientState.LocalPlayer?.Position;
        if (playerPos == null) return new List<IBattleChara>();

        return Svc.Objects.OfType<IBattleChara>()
                  .Where(bc => Vector3.DistanceSquared(bc.Position, playerPos.Value) <= range * range)
                  .ToList();
    }
    
    /// <summary>
    /// 检查指定对象是否为敌人
    /// </summary>
    /// <param name="obj">要检查的游戏对象</param>
    /// <returns>是否为敌人</returns>
    public unsafe bool IsEnemy(IGameObject obj)
    {
        return obj != null && ActionManager.CanUseActionOnTarget(7U, obj.Struct());
    }
    
    /// <summary>
    /// 计算目标与玩家之间的距离
    /// </summary>
    /// <param name="obj">目标对象</param>
    /// <returns>距离，如果无法计算则返回最大值</returns>
    public float DistanceToPlayer(IGameObject? obj)
    {
        if (obj == null)
            return float.MaxValue;
            
        IPlayerCharacter playerCharacter = Player.Object;
        return playerCharacter == null ? 
            float.MaxValue : 
            Vector3.Distance(playerCharacter.Position, obj.Position) - obj.HitboxRadius;
    }
    
    /// <summary>
    /// 计算目标与玩家之间的距离（保留一位小数）
    /// </summary>
    /// <param name="obj">目标对象</param>
    /// <returns>距离，如果无法计算则返回最大值</returns>
    public float DistanceToPlayerOne(IGameObject? obj)
    {
        if (obj == null)
            return float.MaxValue;
            
        IPlayerCharacter playerCharacter = Player.Object;
        if (playerCharacter == null)
            return float.MaxValue;
        
        float distance = Vector3.Distance(playerCharacter.Position, obj.Position) - obj.HitboxRadius;
        return float.Parse(distance.ToString("F1"));
    }
    
/// <summary>
    /// 检查目标是否可被驱散
    /// </summary>
    /// <param name="battleCharacter">战斗角色</param>
    /// <returns>是否有可驱散的状态效果</returns>
    public bool HasCanDispel(IBattleChara battleCharacter)
    {
        if (battleCharacter == null)
            return false;
            
        foreach (var status in battleCharacter.StatusList)
        {
            if (Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Status>().GetRow(status.StatusId).CanDispel)
                return true;
        }
        return false;
    }
    
    /// <summary>
    /// 获取附近的敌人列表
    /// </summary>
    /// <param name="range">范围，默认为50</param>
    /// <returns>范围内的敌人列表</returns>
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
                      if (enemy == null || !enemy.IsValid()) return false; // 检查是否为有效目标
                      
                      // 检查距离，使用平方距离优化性能
                      return Vector3.DistanceSquared(enemy.Position, playerPos.Value) <= range * range;
                  })
                  .ToList();
    }

    /// <summary>
    /// 获取范围内优先级最高的标记目标
    /// </summary>
    /// <param name="plugin">插件实例</param>
    /// <param name="range">范围</param>
    /// <param name="core">核心功能实例</param>
    /// <returns>标记目标，如果没有则返回null</returns>
    public static IBattleChara? GetHighestPriorityMarkedTargetInRange(Plugin plugin, float range, Core core)
    {
        var playerPos = Svc.ClientState.LocalPlayer?.Position;
        if (playerPos == null)
        {
            return null;
        }

        var selectedMarker = plugin.Configuration.SelectedHeadMarker;
        
        // 只检查选中的标记
        if (plugin.Configuration.MarkerEnabled.TryGetValue(selectedMarker, out var enabled) && enabled)
        {
            var target = MarkerHelper.GetCharacterByHeadMarker(selectedMarker, core);
            if (target != null && 
                target.IsValid() && 
                Vector3.DistanceSquared(target.Position, playerPos.Value) <= range * range)
            {
                return target;
            }
            else
            {
                return null; // 如果没有找到带有选定标记的目标，则返回null
            }
        }
        else
        {
            return null; // 如果选定的标记被禁用，则返回null
        }
    }
    
    /// <summary>
    /// 检查目标是否在技能攻击范围内且可被技能命中
    /// </summary>
    /// <param name="actionId">技能ID</param>
    /// <param name="target">目标</param>
    /// <returns>是否可命中</returns>
    public static unsafe bool IsTargetVisibleOrInRange(uint actionId, IBattleChara? target)
    {
        if (Svc.ClientState.LocalPlayer != null && target != null && target.IsTargetable)
        { 
            var skillTarget = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)target.Address;
            var me = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)Svc.ClientState.LocalPlayer.Address;
            
            if (ActionManager.GetActionInRangeOrLoS == null)
            {
                return false;
            }
            if (!LineOfSightChecker.IsBlocked(me, skillTarget))//新的视野判断逻辑
                return ActionManager.GetActionInRangeOrLoS(actionId, me, skillTarget) is not (566 or 562);
        }

        return false;
    }
    
    /// <summary>
    /// 小队相关功能类
    /// </summary>
    public static class Party
    {
        /// <summary>
        /// 获取指定角色的小队成员
        /// </summary>
        /// <param name="role">战斗角色</param>
        /// <param name="range">范围，默认为50</param>
        /// <returns>小队成员列表</returns>
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

        /// <summary>
        /// 获取所有小队成员
        /// </summary>
        /// <returns>小队成员列表</returns>
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
                    if (obj is IBattleChara battleChara)
                    {
                        party.Add(battleChara);
                    }
                }
            }
            return party;
        }
    }
    
    /// <summary>
    /// 小队助手类
    /// </summary>
    public static class PartyHelper
    {
        /// <summary>
        /// 获取所有小队成员（按角色分类）
        /// </summary>
        /// <param name="range">范围，默认为50</param>
        /// <returns>所有小队成员列表</returns>
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
        }
    }
}
