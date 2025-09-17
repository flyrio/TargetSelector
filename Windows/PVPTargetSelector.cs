using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Common.Math;
using Lumina.Text;

namespace TargetSelector.Windows
{
    /// <summary>
    /// PVP目标选择器，用于在PVP战斗中智能选择目标
    /// </summary>
    public class PVPTargetSelector
    {
        private readonly Core core;
        private readonly Plugin plugin;

        public PVPTargetSelector(Core core, Plugin plugin)
        {
            this.core = core;
            this.plugin = plugin;
        }

        /// <summary>
        /// 获取指定游戏对象的当前目标
        /// </summary>
        public IGameObject? CurrTarget(IGameObject battleChara)
        {
            if (battleChara == null) return null;
            return Svc.Objects.SearchById(battleChara.TargetObjectId);
        }

        /// <summary>
        /// 获取焦点目标
        /// </summary>
        public IBattleChara? GetFocusTarget()
        {
            if (!core.IsPvP()) return null;
            if (!plugin.Configuration.选择器开关) return null;

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

        /// <summary>
        /// 智能选择圆形AOE技能的最佳目标
        /// </summary>
        public IBattleChara? SmartTargetCircleAOE(int minTargetCount, float maxCastRange, float aoeRadius, uint actionId = 29415)
        {
            var all = core.获取附近所有目标();
            if (all == null) return null;

            List<IBattleChara> enemies = new();
            foreach (var e in all)
            {
                if (e == null || !e.IsValid() || !core.IsEnemy(e)) continue;
                float dist = core.DistanceToPlayer(e);
                if (dist <= maxCastRange + aoeRadius && Core.IsTargetVisibleOrInRange(actionId, e))
                    enemies.Add(e);
            }
            if (enemies.Count == 0) return null;

            IBattleChara? bestTarget = null;
            int bestHitCount = 0;
            uint highestHP = 0;

            foreach (var t in enemies)
            {
                float tRadius = t.HitboxRadius;
                if (core.DistanceToPlayer(t) > maxCastRange + tRadius) continue;

                int hitCount = 0;
                foreach (var e in enemies)
                {
                    if (Vector3.Distance(e.Position, t.Position) <= aoeRadius + e.HitboxRadius)
                        hitCount++;
                }

                if (hitCount > bestHitCount || (hitCount == bestHitCount && t.CurrentHp > highestHP))
                {
                    bestHitCount = hitCount;
                    bestTarget = t;
                    highestHP = t.CurrentHp;
                }
            }

            if (bestTarget != null && bestHitCount >= minTargetCount)
                return bestTarget;

            return null;
        }

        /// <summary>
        /// 获取目标
        /// </summary>
        public IBattleChara? GetTarget()
        {
            if (!core.IsPvP()) return null;
            if (!plugin.Configuration.选择器开关) return null;

            var currentTarget = CurrTarget(core.Me) as IBattleChara;
            if (plugin.Configuration.有目标时不切换目标 &&
                currentTarget != null &&
                core.DistanceToPlayer(currentTarget) <= plugin.Configuration.选中距离)
            {
                return null;
            }

            if (plugin.Configuration.头标开关)
            {
                var markedTarget = Core.GetHighestPriorityMarkedTargetInRange(plugin, plugin.Configuration.选中距离, core);
                if (markedTarget != null) return markedTarget;
            }

            switch (plugin.Configuration.选择模式)
            {
                case MainWindow.TargetSelectMode.最近单位: return TargetSelector.Get最近目标(plugin);
                case MainWindow.TargetSelectMode.最远单位: return TargetSelector.Get最远目标(plugin);
                case MainWindow.TargetSelectMode.血量最少: return TargetSelector.Get最合适目标(plugin);
                default: return null;
            }
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
        public static bool HasAura(IBattleChara battleCharacter, uint id, int timeLeft = 0)
        {
            if (battleCharacter == null) return false;
            var statuses = battleCharacter.StatusList;
            if (statuses == null) return false;

            for (int i = 0; i < statuses.Length; i++)
            {
                var s = statuses[i];
                if (s == null || s.StatusId == 0) continue;
                if (s.StatusId == id)
                {
                    if (timeLeft == 0 || s.RemainingTime * 1000f >= timeLeft)
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 获取血量最少的目标
        /// </summary>
        public static IBattleChara? Get最合适目标(Plugin plugin, uint 技能ID = 29415)
        {
            if (!plugin.core.IsPvP()) return null;

            IBattleChara? best = null;
            uint minHp = int.MaxValue;
            float maxRange = plugin.Configuration.选中距离;

            foreach (var m in plugin.core.获取附近所有目标(maxRange))
            {
                if (m == null || !m.IsTargetable || !plugin.core.IsEnemy(m)) continue;
                if (ShouldExcludeTarget(m, plugin)) continue;
                if (plugin.core.DistanceToPlayer(m) > maxRange) continue;
                if (!Core.IsTargetVisibleOrInRange(技能ID, m)) continue;

                if (m.CurrentHp < minHp)
                {
                    best = m;
                    minHp = m.CurrentHp;
                }
            }
            return best;
        }

        /// <summary>
        /// 获取最近的目标
        /// </summary>
        public static IBattleChara? Get最近目标(Plugin plugin, uint 技能ID = 29415)
        {
            if (!plugin.core.IsPvP()) return null;

            IBattleChara? best = null;
            float bestDist = plugin.Configuration.选中距离;

            foreach (var m in plugin.core.获取附近所有目标(plugin.Configuration.选中距离))
            {
                if (m == null || !m.IsTargetable || !plugin.core.IsEnemy(m)) continue;
                if (ShouldExcludeTarget(m, plugin)) continue;
                float dist = plugin.core.DistanceToPlayer(m);
                if (!Core.IsTargetVisibleOrInRange(技能ID, m)) continue;

                if (dist < bestDist)
                {
                    best = m;
                    bestDist = dist;
                }
            }
            return best;
        }

        /// <summary>
        /// 获取最远的目标
        /// </summary>
        public static IBattleChara? Get最远目标(Plugin plugin, uint 技能ID = 29415)
        {
            if (!plugin.core.IsPvP()) return null;

            IBattleChara? best = null;
            float maxDist = 0f;

            foreach (var m in plugin.core.获取附近所有目标(plugin.Configuration.选中距离))
            {
                if (m == null || !m.IsTargetable || !plugin.core.IsEnemy(m)) continue;
                if (ShouldExcludeTarget(m, plugin)) continue;
                float dist = plugin.core.DistanceToPlayer(m);
                if (!Core.IsTargetVisibleOrInRange(技能ID, m)) continue;

                if (dist > maxDist)
                {
                    best = m;
                    maxDist = dist;
                }
            }
            return best;
        }

        /// <summary>
        /// 检查目标是否应该被排除
        /// </summary>
        public static bool ShouldExcludeTarget(IBattleChara target, Plugin plugin)
        {
            if (plugin.Configuration.排除黑骑无敌 && HasAura(target, 3039u)) return true;
            if (plugin.Configuration.排除被保护目标 && (HasAura(target, 2413u) || HasAura(target, 1301u))) return true;
            if (plugin.Configuration.排除骑士无敌 && HasAura(target, 1302u)) return true;
            if (plugin.Configuration.排除龟壳 && HasAura(target, 3054u)) return true;
            if (plugin.Configuration.排除地天 && HasAura(target, 1240u)) return true;
            return false;
        }
    }
}
