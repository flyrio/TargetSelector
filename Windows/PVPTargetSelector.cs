using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using FFXIVClientStructs.FFXIV.Common.Math;
using Lumina.Text;

namespace TargetSelector.Windows;

/// <summary>
/// PVP目标选择器，用于在PVP战斗中智能选择目标
/// </summary>
public class PVPTargetSelector
{
    private readonly Core core;
    private readonly Plugin plugin;
    
    /// <summary>
    /// 获取指定游戏对象的当前目标
    /// </summary>
    /// <param name="battleChara">要查询的游戏对象</param>
    /// <returns>目标对象，如果没有目标则返回null</returns>
    public IGameObject? CurrTarget(IGameObject battleChara)
    {
        return battleChara == null ? null : Svc.Objects.SearchById(battleChara.TargetObjectId);
    }
    
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="core">核心功能实例</param>
    /// <param name="plugin">插件实例</param>
    public PVPTargetSelector(Core core, Plugin plugin)
    {
        this.core = core;
        this.plugin = plugin;
    }

    /// <summary>
    /// 获取焦点目标
    /// </summary>
    /// <returns>焦点目标，如果没有合适的目标则返回null</returns>
    public IBattleChara? GetFocusTarget()
    {
        try
        {
            var currentTarget = CurrTarget(core.Me) as IBattleChara;
            bool isPvP = this.core.IsPvP();
            
            // 非PVP环境下返回null
            if (!isPvP)
            {
                return null;
            }
            
            // 如果选择器开关关闭，返回null
            if (!this.plugin.Configuration.选择器开关)
            {
                return null;
            }

            // 如果启用了最佳AOE目标功能
            if (plugin.Configuration.最佳AOE目标)
            {
                var focusTarget = SmartTargetCircleAOE(
                    plugin.Configuration.AOE数量,
                    plugin.Configuration.选中距离, 
                    plugin.Configuration.AOE技能伤害范围);
                
                if (focusTarget != null && !TargetSelector.ShouldExcludeTarget(focusTarget, plugin))
                    return focusTarget;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
    
    /// <summary>
    /// 智能选择圆形AOE技能的最佳目标
    /// </summary>
    /// <param name="minTargetCount">最小目标数量</param>
    /// <param name="maxCastRange">最大施法距离</param>
    /// <param name="aoeRadius">AOE半径</param>
    /// <param name="actionId">技能ID</param>
    /// <returns>最佳目标，如果没有合适的目标则返回null</returns>
    public IBattleChara? SmartTargetCircleAOE(
        int minTargetCount, 
        float maxCastRange, 
        float aoeRadius,
        uint actionId = 29415) // 目标为中心的圆形AOE伤害，选择最优目标（同命中数量选血最多的）
    {
        // 获取玩家和敌人列表
        var enemies = core.获取附近所有目标()?
            .Where(e => e != null && 
                   e.IsValid() && 
                   core.IsEnemy(e) && 
                   core.DistanceToPlayer(e) <= maxCastRange + aoeRadius && 
                   Core.IsTargetVisibleOrInRange(actionId, e))
            .ToList() ?? new List<IBattleChara>();
        
        if (!enemies.Any()) return null;

        List<IBattleChara> bestTargets = new List<IBattleChara>();
        int maxHitCount = 0;

        // 遍历每个潜在目标
        foreach (var potentialTarget in enemies)
        {
            // 获取潜在目标的碰撞箱半径
            float potentialTargetRadius = potentialTarget.HitboxRadius;

            // 判断潜在目标是否在施法距离内
            if (core.DistanceToPlayer(potentialTarget) > maxCastRange + potentialTargetRadius) 
                continue;

            // 计算这个潜在目标可以击中的敌人数
            int hitCount = enemies.Count(e => 
                Vector3.Distance(e.Position, potentialTarget.Position) <= aoeRadius + e.HitboxRadius
            );

            // 更新最佳目标列表
            if (hitCount > maxHitCount)
            {
                maxHitCount = hitCount;
                bestTargets.Clear();
                bestTargets.Add(potentialTarget);
            }
            else if (hitCount == maxHitCount)
            {
                bestTargets.Add(potentialTarget);
            }
        }

        // 如果有击中足够多敌人的目标
        if (bestTargets.Any() && maxHitCount >= minTargetCount)
        {
            // 从最佳目标中选择血量最多的
            return bestTargets.OrderByDescending(t => t.CurrentHp).FirstOrDefault();
        }

        // 如果没有找到满足条件的目标，返回null
        return null;
    }
    
    /// <summary>
    /// 获取目标
    /// </summary>
    /// <returns>选中的目标，如果没有合适的目标则返回null</returns>
    public IBattleChara GetTarget()
    { 
        try 
        { 
            // 非PVP环境下返回null
            bool isPvP = this.core.IsPvP();
            if (!isPvP)
            {
                return null;
            }

            // 如果选择器开关关闭，返回null
            if (!this.plugin.Configuration.选择器开关)
            {
                return null;
            }

            // 获取当前目标
            var currentTarget = CurrTarget(core.Me) as IBattleChara;

            // 如果设置了有目标时不切换目标，且当前有有效目标，返回null
            if (this.plugin.Configuration.有目标时不切换目标 && 
                currentTarget != null && 
                core.DistanceToPlayer(currentTarget) <= plugin.Configuration.选中距离)
            {
                return null;
            }

            // 如果启用了头标优先，检查是否有标记的目标
            if (this.plugin.Configuration.头标开关)
            {
                var markedTarget = Core.GetHighestPriorityMarkedTargetInRange(
                    this.plugin, 
                    this.plugin.Configuration.选中距离, 
                    this.core);
                
                if (markedTarget != null)
                {
                    return markedTarget;
                }
            }

// 根据选择模式选择目标
        switch (this.plugin.Configuration.选择模式)
        {
            case MainWindow.TargetSelectMode.最近单位:
                return TargetSelector.Get最近目标(this.plugin);
                
            case MainWindow.TargetSelectMode.最远单位:
                return TargetSelector.Get最远目标(this.plugin);
                
            case MainWindow.TargetSelectMode.血量最少:
                return TargetSelector.Get最合适目标(this.plugin);
                
            default:
                return null;
        }
    }
    catch (Exception ex)
    {
        // 发生异常时返回null
        Console.WriteLine($"GetTarget 错误: {ex.Message}");
        return null;
    }
}

/// <summary>
/// 目标选择器辅助类
/// </summary>
public static class TargetSelector
{
    /// <summary>
    /// 检查目标是否有指定的状态效果
    /// </summary>
    /// <param name="battleCharacter">战斗角色</param>
    /// <param name="id">状态效果ID</param>
    /// <param name="timeLeft">剩余时间阈值（毫秒）</param>
    /// <returns>是否有指定状态效果</returns>
    public static bool HasAura(IBattleChara battleCharacter, uint id, int timeLeft = 0)
    {
        if (battleCharacter == null)
            return false;
            
        for (int index = 0; index < battleCharacter.StatusList.Length; ++index)
        {
            Dalamud.Game.ClientState.Statuses.Status status = battleCharacter.StatusList[index];
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
    /// 获取血量最少的目标
    /// </summary>
    /// <param name="plugin">插件实例</param>
    /// <param name="技能ID">技能ID</param>
    /// <returns>血量最少的目标</returns>
    public static IBattleChara? Get最合适目标(Plugin plugin, uint 技能ID = 29415)
    {
        try
        {
            if (!plugin.core.IsPvP())
            {
                return null;
            }

            IBattleChara? 最合适的目标 = null;
            float 当前最低血量 = float.MaxValue;

            foreach (var member in plugin.core.获取附近所有目标(plugin.Configuration.选中距离))
            {
                if (member == null || !member.IsTargetable || !plugin.core.IsEnemy(member))
                    continue;
                    
                if (ShouldExcludeTarget(member, plugin))
                {
                    continue;
                }
                
                float distance = plugin.core.DistanceToPlayer(member);
                if (!Core.IsTargetVisibleOrInRange(技能ID, member))
                    continue;
                    
                if (distance > plugin.Configuration.选中距离)
                    continue;

                if (member.CurrentHp < 当前最低血量)
                {
                    最合适的目标 = member;
                    当前最低血量 = member.CurrentHp;
                }
            }
            
            return 最合适的目标;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Get最合适目标 错误: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// 获取最近的指定职业目标
    /// </summary>
    /// <param name="plugin">插件实例</param>
    /// <param name="技能ID">技能ID</param>
    /// <returns>最近的黑骑/镰刀/舞者目标</returns>
    public static IBattleChara? Get黑骑镰刀舞者目标(Plugin plugin, uint 技能ID)
    {
        if (!plugin.core.IsPvP())
        {
            return null;
        }
        
        IBattleChara? 最合适的目标 = null;
        
        foreach (var member in plugin.core.获取附近所有目标())
        {
            if (member == null || !member.IsTargetable || !plugin.core.IsEnemy(member))
                continue;

            try
            { 
                // 只选择黑骑、镰刀、舞者职业
                if (member.ClassJob.Value.RowId != (uint)Job.DRK && 
                    member.ClassJob.Value.RowId != (uint)Job.RPR && 
                    member.ClassJob.Value.RowId != (uint)Job.DNC)
                    continue;
                    
                float distance = plugin.core.DistanceToPlayer(member); 
                if (!Core.IsTargetVisibleOrInRange(技能ID, member))
                    continue;
                    
                if (distance > plugin.Configuration.选中距离) 
                    continue;
                    
                最合适的目标 = member;
            }
            catch (Exception)
            {
                // 忽略处理异常
            }
        }
        
        return 最合适的目标;
    }
    
    /// <summary>
    /// 获取可以被康复的小队成员
    /// </summary>
    /// <param name="plugin">插件实例</param>
    /// <returns>可被康复的小队成员</returns>
    public static IBattleChara? Get康复目标(Plugin plugin)
    {
        if (!plugin.core.IsPvP())
        {
            return null;
        }
        
        IBattleChara? 康复目标 = null;
        
        foreach (var member in Core.PartyHelper.GetAllPartyMembersByRole(plugin.Configuration.选中距离))
        {
            // 移除敌对判断，添加保护性buff判断
            if (member == null || !member.IsTargetable || plugin.core.HasAura(member, 1302))
                continue;

            // 只选择可以被驱散的目标
            if (!plugin.core.HasCanDispel(member))
                continue;

            float distance = plugin.core.DistanceToPlayer(member);
            if (distance > plugin.Configuration.选中距离)
                continue;

// 找到即可返回，无需遍历所有成员
                康复目标 = member;
                break; // 找到后立即跳出循环
            }
            
            return 康复目标;
        }
        
        /// <summary>
        /// 获取距离最近的敌对目标
        /// </summary>
        /// <param name="plugin">插件实例</param>
        /// <param name="技能ID">技能ID</param>
        /// <returns>距离最近的敌对目标</returns>
        public static IBattleChara? Get最近目标(Plugin plugin, uint 技能ID = 29415)
        {
            if (!plugin.core.IsPvP())
            {
                return null;
            }

            IBattleChara? 最近的目标 = null;
            float 最近距离 = plugin.Configuration.选中距离; // 使用配置中的距离
            
            foreach (var member in plugin.core.获取附近所有目标(plugin.Configuration.选中距离))
            {
                if (member == null || !member.IsTargetable || !plugin.core.IsEnemy(member))
                    continue;
                    
                if (ShouldExcludeTarget(member, plugin))
                {
                    continue;
                }
                
                float distance = plugin.core.DistanceToPlayer(member);
                if (!Core.IsTargetVisibleOrInRange(技能ID, member))
                    continue;
                    
                if (distance < 最近距离) // 移除<=，避免选中自己
                {
                    最近的目标 = member;
                    最近距离 = distance;
                }
            }
            
            return 最近的目标;
        }

        /// <summary>
        /// 获取距离最远的敌对目标
        /// </summary>
        /// <param name="plugin">插件实例</param>
        /// <param name="技能ID">技能ID</param>
        /// <returns>距离最远的敌对目标</returns>
        public static IBattleChara? Get最远目标(Plugin plugin, uint 技能ID = 29415)
        {
            if (!plugin.core.IsPvP())
            {
                return null;
            }

            IBattleChara? 最远的目标 = null;
            float 最远距离 = 0f;

            foreach (var member in plugin.core.获取附近所有目标(plugin.Configuration.选中距离))
            {
                if (member == null || !member.IsTargetable || !plugin.core.IsEnemy(member))
                    continue;
                    
                if (ShouldExcludeTarget(member, plugin))
                {
                    continue;
                }
                
                float distance = plugin.core.DistanceToPlayer(member);
                if (!Core.IsTargetVisibleOrInRange(技能ID, member))
                    continue;
                    
                if (distance > 最远距离)
                {
                    最远的目标 = member;
                    最远距离 = distance;
                }
            }
            
            return 最远的目标;
        }

        /// <summary>
        /// 检查目标是否应该被排除
        /// </summary>
        /// <param name="target">目标</param>
        /// <param name="plugin">插件实例</param>
        /// <returns>是否应该排除目标</returns>
        public static bool ShouldExcludeTarget(IBattleChara target, Plugin plugin)
        {
            // 如果配置开启且目标有对应buff，则返回true表示应该排除该目标
            if (plugin.Configuration.排除黑骑无敌 && HasAura(target, 3039u)) return true;
            if (plugin.Configuration.排除被保护目标 && (HasAura(target, 2413u) || HasAura(target, 1301u))) return true;
            if (plugin.Configuration.排除骑士无敌 && HasAura(target, 1302u)) return true;
            if (plugin.Configuration.排除龟壳 && HasAura(target, 3054u)) return true;
            if (plugin.Configuration.排除地天 && HasAura(target, 1240u)) return true;

            return false; // 不需要排除该目标
        }
    }
}
