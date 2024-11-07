using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.ComTypes;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.GameFunctions;
using ECommons.Logging;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace TargetSelector.Windows;

public class PVPTargetSelector
{
    private readonly Core core;
    private readonly Plugin plugin;
    

    
    public IGameObject? CurrTarget(IGameObject battleChara)
    {
        return battleChara == null ? (IGameObject) null : Svc.Objects.SearchById(battleChara.TargetObjectId) ?? (IGameObject) null;
    }
    public PVPTargetSelector(Core core, Plugin plugin)
    {
        this.core = core;
        this.plugin = plugin;
    }
    
    
    public IBattleChara? GetTarget()
    { 
        try 
        { 
            bool isPvP = this.core.IsPvP();
            if (!isPvP)
            {
                return null;
            }

            if (!this.plugin.Configuration.选择器开关)
            {
                //PluginLog.LogDebug($"选择器开关关闭，返回 null"); // 添加日志说明原因
                return null;
            }


            var currentTarget = CurrTarget(core.Me) as IBattleChara;
            //PluginLog.LogDebug($"我当前的目标是不是敌人{core.IsEnemy(currentTarget)}"); // 添加日志说明原因
            //PluginLog.LogDebug($"当前目标: {(currentTarget == null ? "null" : currentTarget.Name)}"); // 记录当前目标信息


            if (this.plugin.Configuration.有目标时不切换目标 && currentTarget != null && core.DistanceToPlayer(currentTarget) <= plugin.Configuration.选中距离)
            {
                //PluginLog.LogDebug($"有目标时不切换目标 启用，且当前有目标，返回 null"); // 添加日志说明原因
                return null;
            }

            // if (this.plugin.Configuration.选中友方单位时不切换目标 && currentTarget != null && (!core.IsEnemy(currentTarget) || currentTarget == core.Me))
            // {
            //     PluginLog.LogDebug($"选中友方单位时不切换目标 启用，且当前目标是友方，返回 null"); // 添加日志说明原因
            //     return null;
            // }

            if (this.plugin.Configuration.头标开关)
            {
                var markedTarget = Core.GetHighestPriorityMarkedTargetInRange(this.plugin, this.plugin.Configuration.选中距离, this.core);
                //PluginLog.LogDebug($"头标目标: {(markedTarget == null ? "null" : markedTarget.Name)}"); // 记录头标目标信息
                if (markedTarget != null)
                {
                    //PluginLog.LogDebug($"返回头标目标"); // 添加日志说明原因
                    return markedTarget;
                }
            } // PluginLog.LogDebug($"选择模式: {this.plugin.Configuration.选择模式}");

            switch (this.plugin.Configuration.选择模式)
            {
                case MainWindow.TargetSelectMode.最近单位:
                    var nearestTarget = TargetSelector.Get最近目标(this.plugin);
                   // PluginLog.LogDebug($"最近目标: {(nearestTarget == null ? "null" : nearestTarget.Name)}"); // 记录最近目标信息
                    return nearestTarget;
                case MainWindow.TargetSelectMode.最远单位:
                    var farthestTarget = TargetSelector.Get最远目标(this.plugin);
                   // PluginLog.LogDebug($"最远目标: {(farthestTarget == null ? "null" : farthestTarget.Name)}"); // 记录最远目标信息
                    return farthestTarget;
                case MainWindow.TargetSelectMode.血量最少:
                    var lowestHpTarget = TargetSelector.Get最合适目标(this.plugin);
                   // PluginLog.LogDebug($"血量最少目标: {(lowestHpTarget == null ? "null" : lowestHpTarget.Name)}"); // 记录血量最少目标信息
                    return lowestHpTarget;
                default:
                   // PluginLog.LogDebug($"选择模式未定义，返回 null"); // 添加日志说明原因
                    return null;
            }
        }
        catch (Exception ex)
        {
            try
            {
               // PluginLog.Log($"GetTarget 错误: {ex}");
               // PluginLog.Log($"GetTarget 错误详情: {ex.Message}, 堆栈跟踪: {ex.StackTrace}"); // 记录更详细的错误信息
            }
            catch (Exception innerEx)
            {
                // 使用 Console.WriteLine 或其他可靠的日志方法作为备用方案
                Console.WriteLine($"记录 GetTarget 错误时发生错误: {innerEx}");
                Console.WriteLine($"内部异常堆栈跟踪: {innerEx.StackTrace}");
            }
            return null;
        }
    }

    public static class TargetSelector
    {
        public static bool HasAura(IBattleChara battleCharacter, uint id, int timeLeft = 0)
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
        
        public static IBattleChara? Get最合适目标(Plugin plugin,uint 技能ID = 29415)
        {
            try
            {
                if (!plugin.core.IsPvP())
                {
                    return null;
                }

                IBattleChara? 最合适的目标 = null; // 初始化为 null
                float 当前最低血量 = float.MaxValue;

                foreach (var member in plugin.core.获取附近所有目标(plugin.Configuration.选中距离))
                {
                    if (member == null || !member.IsTargetable || !plugin.core.IsEnemy(member)) // 使用辅助方法
                        continue;
                    //不死救赎3039 神圣领域1302 被保护2413 龟壳3054 地天1240 被保护1301?
                    if ((HasAura(member, 3039u) && plugin.Configuration.排除黑骑无敌) || (HasAura(member, 2413u)  && plugin.Configuration.排除被保护目标) || (HasAura(member, 1301u) && plugin.Configuration.排除被保护目标) || (HasAura(member, 1302u) && plugin.Configuration.排除骑士无敌))
                        continue;
                    if ((HasAura(member, 3054u) && plugin.Configuration.排除龟壳) || (HasAura(member, 1240u) && plugin.Configuration.排除地天))
                        continue;
                    float distance = plugin.core.DistanceToPlayer(member);
                    if (!Core.IsTargetVisibleOrInRange(技能ID,member))
                        continue;
                    if (distance > plugin.Configuration.选中距离)
                        continue;

                    if (member.CurrentHp < 当前最低血量)
                    {
                        最合适的目标 = member;
                        当前最低血量 = member.CurrentHp;
                    }
                }
                //PluginLog.LogDebug($"Get最合适目标 找到的目标: {最合适的目标?.Name ?? "null"}"); // 记录找到的目标
                return 最合适的目标;
            }
            catch (Exception ex) 
            {
                //PluginLog.LogDebug($"Get最合适目标 错误: {ex}"); // 记录错误信息
                return null;
            }
        }
		
            /// <summary>
            /// 获取最近的指定职业目标
            /// </summary>
            /// <param name="技能距离">技能作用距离</param>
            /// <returns>血量最低的目标</returns>
            public static IBattleChara? Get黑骑镰刀舞者目标(Plugin plugin,uint 技能ID)
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
                        if(member.ClassJob.Id != (uint)Job.DRK && member.ClassJob.Id != (uint)Job.RPR && member.ClassJob.Id != (uint)Job.DNC)
                            continue;
                        float distance = plugin.core.DistanceToPlayer(member); 
                        if (!Core.IsTargetVisibleOrInRange(技能ID,member))
                            continue;
                        if (distance > plugin.Configuration.选中距离) 
                            continue;
                        最合适的目标 = member;
                    }
                    catch (Exception ex)
                    {
                        // ignored
                    }
                }
                return 最合适的目标;
            }
				
            /// <summary>
            /// 获取可以被康复的小队成员
            /// </summary>
            /// <returns>距离最近的目标</returns>
            public static IBattleChara? Get康复目标(Plugin plugin)
            {
                if (!plugin.core.IsPvP())
                {
                    return null;
                }
                IBattleChara? 康复目标 = null;
                foreach (var member in Core.PartyHelper.GetAllPartyMembersByRole(plugin.Configuration.选中距离)) // 使用配置中的距离
                {
                    if (member == null || !member.IsTargetable || plugin.core.HasAura(member,1302)) //移除敌对判断，添加保护性buff判断
                        continue;

                    if (!plugin.core.HasCanDispel(member)) // 只选择可以被驱散的目标
                        continue;

                    float distance = plugin.core.DistanceToPlayer(member);
                    if (distance > plugin.Configuration.选中距离)
                        continue;


                    康复目标 = member; //  找到即可返回，无需遍历所有成员
                    break;         //  找到后立即跳出循环
                }
                //PluginLog.LogDebug($"Get康复目标 找到的目标: {康复目标?.Name ?? "null"}"); // 记录找到的目标
                return 康复目标;
            }
            public static IBattleChara? Get最近目标(Plugin plugin ,uint 技能ID = 29415)
            {
                if (!plugin.core.IsPvP())
                {
                    return null;
                }

                IBattleChara? 最近的目标 = null;             // 初始化为 null
                float 最近距离 = plugin.Configuration.选中距离; // 使用配置中的距离
                var targets = plugin.core.获取附近所有目标(plugin.Configuration.选中距离);
                //PluginLog.Log($"附近目标数量: {targets.Count}"); // 输出目标数量
                
                foreach (var member in plugin.core.获取附近所有目标(plugin.Configuration.选中距离))
                {
                    //PluginLog.Log($"目标 {member?.Name ?? "null"}: IsTargetable={member?.IsTargetable ?? false}, IsEnemy={plugin.core.IsEnemy(member)}, HasAura(1302)={plugin.core.HasAura(member, 1302)}"); // 输出目标信息，处理 member 为 null 的情况
                    if (member == null || !member.IsTargetable || !plugin.core.IsEnemy(member))
                        continue;
                    //不死救赎3039 神圣领域1302 被保护2413 龟壳3054 地天1240 被保护1301?
                    if ((HasAura(member, 3039u) && plugin.Configuration.排除黑骑无敌) || (HasAura(member, 2413u)  && plugin.Configuration.排除被保护目标) || (HasAura(member, 1301u) && plugin.Configuration.排除被保护目标) || (HasAura(member, 1302u) && plugin.Configuration.排除骑士无敌))
                        continue;
                    if ((HasAura(member, 3054u) && plugin.Configuration.排除龟壳) || (HasAura(member, 1240u) && plugin.Configuration.排除地天))
                        continue;
                    float distance = plugin.core.DistanceToPlayer(member);
                    if (!Core.IsTargetVisibleOrInRange(技能ID,member))
                        continue;
                    if (distance < 最近距离) //  移除<=，避免选中自己
                    {
                        最近的目标 = member;
                        最近距离 = distance;
                    }
                }
                //PluginLog.Log($"Get最近目标 找到的目标: {最近的目标?.Name ?? "null"}"); // 记录找到的目标
                return 最近的目标;
            }

            public static IBattleChara? Get最远目标(Plugin plugin,uint 技能ID = 29415)
            {
                if (!plugin.core.IsPvP())
                {
                    return null;
                }

                IBattleChara? 最远的目标 = null; // 初始化为 null
                float 最远距离 = 0f;

                foreach (var member in plugin.core.获取附近所有目标(plugin.Configuration.选中距离)) // 使用配置中的距离
                {
                    if (member == null || !member.IsTargetable || !plugin.core.IsEnemy(member))
                        continue;
                    //不死救赎3039 神圣领域1302 被保护2413 龟壳3054 地天1240 被保护1301?
                    if ((HasAura(member, 3039u) && plugin.Configuration.排除黑骑无敌) || (HasAura(member, 2413u)  && plugin.Configuration.排除被保护目标) || (HasAura(member, 1301u) && plugin.Configuration.排除被保护目标) || (HasAura(member, 1302u) && plugin.Configuration.排除骑士无敌))
                        continue;
                    if ((HasAura(member, 3054u) && plugin.Configuration.排除龟壳) || (HasAura(member, 1240u) && plugin.Configuration.排除地天))
                        continue;
                    float distance = plugin.core.DistanceToPlayer(member);
                    if (!Core.IsTargetVisibleOrInRange(技能ID,member))
                        continue;
                    if (distance > 最远距离)
                    {
                        最远的目标 = member;
                        最远距离 = distance;
                    }
                }
               // PluginLog.LogDebug($"Get最远目标 找到的目标: {最远的目标?.Name ?? "null"}"); // 记录找到的目标
                return 最远的目标;
            }
        }
}
