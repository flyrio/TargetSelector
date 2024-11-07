using System;
using System.Collections.Generic;
using System.Numerics;
using System.Diagnostics;
using System.Linq;
using AEAssist;
using AEAssist.CombatRoutine;
using AEAssist.CombatRoutine.Module;
using AEAssist.CombatRoutine.Module.AILoop;
using AEAssist.CombatRoutine.Module.Target;
using AEAssist.CombatRoutine.View.JobView;
using AEAssist.Define;
using AEAssist.Extension;
using AEAssist.Helper;
using AEAssist.MemoryApi;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface.Textures.TextureWraps;
using ECommons.DalamudServices;
using ImGuiNET;
using AEAssist.Verify;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface.Utility.Raii;
using Lumina.Excel.GeneratedSheets;
using Dalamud.Utility;
using ECommons;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI;
using Shiyuvi;
using CSFramework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;
using SpellsDefine = Linto.LintoPvP.PVPApi.PVPApi.Data.SpellsDefine;

namespace Linto.LintoPvP.PVPApi;

public class PVPHelper
{ 
	//抄来的
	public static Vector3 向量位移(Vector3 position, float facingRadians, float distance) 
	{
	// 计算x-z平面上移动的距离分量  
	float dx = (float)(Math.Sin(facingRadians) * distance);
	float dz = (float)(Math.Cos(facingRadians) * distance);

	return new Vector3(position.X + dx, position.Y + 5, position.Z + dz); 
	}
	private static unsafe RaptureAtkModule* RaptureAtkModule => CSFramework.Instance()->GetUIModule()->GetRaptureAtkModule();

	internal static unsafe float GetCameraRotation()
	{
		// Gives the camera rotation in deg between -180 and 180
		var cameraRotation = RaptureAtkModule->AtkModule.AtkArrayDataHolder.NumberArrays[24]->IntArray[3];

		// Transform the [-180,180] rotation to rad with same 0 as a GameObject rotation
		// There might be an easier way to do that, but geometry and I aren't friends
		var sign = Math.Sign(cameraRotation) == -1 ? -1 : 1;
		var rotation = (float)((Math.Abs(cameraRotation * (Math.PI / 180)) - Math.PI) * sign);

		return rotation;
	}
	
	// //public static Dictionary<string, uint> selectedValuesdb = new Dictionary<string, uint>()
	// {
	// 	{"眩晕", 1343}, 
	// 	{"止步", 1345},
	// 	{"睡眠", 1348},
	// }

	/// <summary>
	/// 判断是否需要用净化 有BUFF为True 无BUFF为False
	/// </summary>
	public static bool 净化判断()
	{
		if (Core.Me.HasAura(1343) || Core.Me.HasAura(1345) || Core.Me.HasAura(1348))
		{
			return true;
		}

		return false;
	}

	public static Spell 不等服务器Spell(uint id, IBattleChara? target)
	{
		var spell = (new Spell(id, target));
		spell.WaitServerAcq = false;
		return spell;
	}

	public static Spell 等服务器Spell(uint id, IBattleChara? target)
	{
		var spell = (new Spell(id, target));
		spell.WaitServerAcq = true;
		return spell;
	}

	/// <summary>
	/// 你的CID
	/// </summary>
	public static List<ulong> 通用码权限列表 = new List<ulong>
	{
		18014479510447116,
		18014449513747129,
		
	};

	public static bool 通用码权限 => 通用码权限列表.Contains(Svc.ClientState.LocalContentId);
	public static bool 高级码 => Share.VIP.Level != VIPLevel.Normal;

	public static bool 通用权限 => 高级码 || 通用码权限;
	public static bool check坐骑() => Svc.Condition[ConditionFlag.Mounted];
	
	/// <summary>
	/// 行动状态检测
	/// </summary>
	/// <param name="检查在不在屁威屁">1</param>
	/// <param name="检查有没有权限">2</param>
	/// <param name="检查是不是在吃药">3</param>
	/// <param name="检查是不是在用坐骑">4</param>
	/// <param name="检查是不是在用龟壳">5</param>
	/// <param name="检查是不是已经上了坐骑">6</param>
	/// <param name="检查是不是3秒内用过龟壳">7</param>
	public static bool CanActive()
	{
		uint 技能龟壳 = 29711u;
		//	if (!Enum.IsDefined(typeof(ZoneId), GetZoneId()))
		if (!Core.Me.IsPvP())
		{
			return false;
		}

		if (!通用码权限 && !高级码)
		{
			return false;
		}

		if (Core.Me.CastActionId == 29055)
		{
			return false;
		}

		if (Core.Me.CastActionId == 4)
		{
			return false;
		}

		if (Core.Me.HasAura(3054u))
		{
			return false;
		}

		if (check坐骑())
		{
			return false;
		}

		if (技能龟壳.RecentlyUsed(3000))
		{
			return false;
		}

		return true;
	}

	private static IBattleChara? Target;

	public static bool HasBuff(IBattleChara BattleChara, uint buffId)
	{
		return BattleChara.HasAura(buffId, 0);
	}

	public static void 技能图标(uint id)
	{
		uint skillid = id;
		Vector2 size1 = new Vector2(60, 60);
		IDalamudTextureWrap? textureWrap;
		if (!Core.Resolve<MemApiIcon>().GetActionTexture(skillid, out textureWrap))
			return;
		ImGui.Image(textureWrap.ImGuiHandle, size1);
	}
	
	

	private static void s1()
	{
		ImGui.PushItemWidth(100);
		ImGui.InputInt($"米内有敌人时不用##{228}", ref PvPSettings.Instance.无目标坐骑范围);
		ImGui.PopItemWidth();
	}

	private static void s2()
	{
		ImGui.PushItemWidth(100);
		ImGui.InputInt($"米内最近目标##{229}", ref PvPSettings.Instance.自动选中自定义范围);
		ImGui.PopItemWidth();
	}

	public static void 通用设置配置()
	{
		ImGui.Text("共通配置");
		ImGui.PushItemWidth(100);
		ImGui.Checkbox($"播报(玩具功能)##{888}", ref PvPSettings.Instance.播报);
		ImGui.Checkbox($"无目标也喝热水##{233}", ref PvPSettings.Instance.脱战嗑药);
		if (ImGui.InputInt($"攻击判定增加(仅影响ACR判断,配合长臂猿使用)##{66}", ref PvPSettings.Instance.长臂猿, 1, 1))
		{
			PvPSettings.Instance.长臂猿 = Math.Clamp(PvPSettings.Instance.长臂猿, 0, 5);
		}

		ImGui.Text("自动选中默认排除:" +
		           "\n不死救赎3039 神圣领域1302 被保护2413 龟壳3054 地天1240");
		ImGui.Checkbox($"自动选中##{666}", ref PvPSettings.Instance.自动选中);
		ImGui.SameLine();
		s2();
		//ImGui.Checkbox($"无目标挂机时冲刺(测试)##{232}", ref PvPSettings.Instance.无目标冲刺);
		//ImGui.SameLine();
		ImGui.Checkbox($"无目标时自动坐骑(默认随机)测试##{222}", ref PvPSettings.Instance.无目标坐骑);
		ImGui.Text($"自动坐骑在范围");
		ImGui.SameLine();
		s1();
		ImGui.Checkbox($"!!技能自动对最近敌人释放!!##{212}", ref PvPSettings.Instance.技能自动选中);
		if (PvPSettings.Instance.技能自动选中)
		{
			ImGui.SameLine();
			ImGui.Checkbox($"!!选择技能范围内血量最低目标!!##{216}", ref PvPSettings.Instance.最合适目标);
		}
		ImGui.PopItemWidth();
		if (ImGui.Button("打榜不如买榜"))
		{
			Core.Resolve<MemApiChatMessage>()
				.Toast2(
					$"当前区域: {Core.Resolve<MemApiMap>().ZoneName(Core.Resolve<MemApiMap>().GetCurrTerrId())} ", 1,
					1500);
		}
	}

	public class 龟壳 : IHotkeyResolver
	{
		public void Draw(Vector2 size)
		{
			Vector2 size1 = size * 0.8f;
			ImGui.SetCursorPos(size * 0.1f);
			IDalamudTextureWrap textureWrap;
			if (!Core.Resolve<MemApiIcon>().GetActionTexture(29054u, out textureWrap))
				return;
			ImGui.Image(textureWrap.ImGuiHandle, size1);

		}

		public void DrawExternal(Vector2 size, bool isActive) =>
			SpellHelper.DrawSpellInfo(new Spell(29054u, Core.Me), size, isActive);

		public int Check() => 0;

		public void Run()
		{
			if (AI.Instance.BattleData.NextSlot == null)
				AI.Instance.BattleData.NextSlot = new Slot();
			if (!Core.Me.HasLocalPlayerAura(3054u) & Core.Me.InCombat())
			{
				AI.Instance.BattleData.NextSlot.Add(new Spell(29054u, Core.Me));
			}
		}
	}
		public static void 权限获取()
		{
			ulong cid = Svc.ClientState.LocalContentId;
			string CID = cid.ToString();
			ImGui.Text($"当前的码等级：[{Share.VIP.Level}]");
			if (!PVPHelper.通用码权限 && !PVPHelper.高级码)
			{
				ImGui.TextColored(new Vector4(255f / 255f, 0f / 255f, 0f / 255f, 0.8f), "无权限");
			}

			if (通用码权限 || 高级码)
			{
				ImGui.TextColored(new Vector4(42f / 255f, 215f / 255f, 57f / 255f, 0.8f), "已解锁");
			}
		}

		public static void 技能配置(uint 技能图标id, string 技能名字, string 描述文字, ref bool 切换配置, int id)
		{
			ImGui.Separator();
			ImGui.Columns(2, null, false);
			ImGui.SetColumnWidth(0, 70);
			技能图标(技能图标id);
			ImGui.NextColumn();
			ImGui.SetColumnWidth(1, 150);
			ImGui.Text(技能名字);
			ImGui.Text($"{描述文字}: {切换配置}");
			if (ImGui.Button($"切换##{id}"))
			{
				切换配置 = !切换配置;
			}

			ImGui.Columns(1);
		}

		public static void 技能配置2(uint 技能图标id, string 技能名字, string 描述文字, ref bool 切换配置, int id)
		{
			ImGui.Separator();
			ImGui.Columns(2, null, false);
			ImGui.SetColumnWidth(0, 70);
			技能图标(技能图标id);
			ImGui.NextColumn();
			ImGui.SetColumnWidth(1, 150);
			ImGui.Text(技能名字);
			ImGui.Text($"{描述文字}:");
			ImGui.Checkbox($"##{id}", ref 切换配置);
			ImGui.Columns(1);
		}

		public static void 技能配置3(uint 技能图标id, string 技能名字, string 描述文字, ref int 数值, int 幅度, int 快速幅度, int id)
		{
			ImGui.Separator();
			ImGui.Columns(2, null, false);
			ImGui.SetColumnWidth(0, 70);
			技能图标(技能图标id);
			ImGui.NextColumn();
			ImGui.SetColumnWidth(1, 150);
			ImGui.Text(技能名字);
			ImGui.Text($"{描述文字}:");
			ImGui.InputInt($"##{id}", ref 数值, 幅度, 快速幅度);
			ImGui.Columns(1);
		}

		public static void 技能配置4(uint 技能图标id, string 技能名字, string 数值描述, string 描述文字, ref bool 切换配置, ref int 数值, int 幅度,
			int 快速幅度, int id)
		{
			ImGui.Separator();
			ImGui.Columns(2, null, false);
			ImGui.SetColumnWidth(0, 70);
			技能图标(技能图标id);
			ImGui.NextColumn();
			ImGui.SetColumnWidth(1, 150);
			ImGui.Text(技能名字);
			ImGui.Text($"{描述文字}:");
			ImGui.Checkbox($"##{id}", ref 切换配置);
			ImGui.Text($"{数值描述}:");
			ImGui.InputInt($"##{id}+1", ref 数值, 幅度, 快速幅度);
			ImGui.Columns(1);
		}

		public static void 技能配置5(uint 技能图标id, string 技能名字, string IntDescription, ref float value, float min, float max,
			int id)
		{
			ImGui.Separator();
			ImGui.Columns(2, null, false);
			ImGui.SetColumnWidth(0, 70);
			技能图标(技能图标id);
			ImGui.NextColumn();
			ImGui.SetColumnWidth(1, 150);
			ImGui.Text(技能名字);
			ImGui.Text($"{IntDescription}:");
			ImGui.SliderFloat($"##{id}", ref value, min, max);
			ImGui.Columns(1);
		}

		public static Spell? 通用技能释放Check(uint skillid, int 距离)
		{
			if (PvPSettings.Instance.技能自动选中)
			{
				if (PvPSettings.Instance.最合适目标 &&
				    (PVPTargetHelper.TargetSelector.Get最合适目标(距离 + PvPSettings.Instance.长臂猿) != null &&
				     PVPTargetHelper.TargetSelector.Get最合适目标(距离 + PvPSettings.Instance.长臂猿) != Core.Me))
				{
					return PVPHelper.等服务器Spell(skillid,
						PVPTargetHelper.TargetSelector.Get最合适目标(距离 + PvPSettings.Instance.长臂猿));
				}

				if ((PVPTargetHelper.TargetSelector.Get最近目标() != null &&
				     PVPTargetHelper.TargetSelector.Get最近目标() != Core.Me))
				{
					return PVPHelper.等服务器Spell(skillid, PVPTargetHelper.TargetSelector.Get最近目标());
				}
			}

			if (Core.Me.GetCurrTarget() != null && Core.Me.GetCurrTarget() != Core.Me)
			{
				return PVPHelper.等服务器Spell(skillid, Core.Me.GetCurrTarget());
			}

			return null;
		}

		// public static bool 返回有buff的目标(int 检测范围)
		// {
		// 	Dictionary<uint, IBattleChara> all = new Dictionary<uint, IBattleChara>();
		//
		// 	foreach (IBattleChara unit in Svc.Objects.OfType<IBattleChara>())
		// 	{
		// 		if (unit.EntityId != 0) continue;
		// 		if (unit.EntityId != Core.Me.EntityId) continue;
		// 		var unitDis = Core.Me.Distance(unit);
		//
		// 		if (unitDis <= 检测范围)
		// 		{
		// 			all[unit.EntityId] = unit;
		// 		}//检索范围内所有目标
		//
		// 		var message = NetworkHelper.EventActionOpcode == 1;
		// 		var target = all.Any(e => e.Value..以太步.RecentlyUsed())
		// 	}
		// }
		
		public static int Get目标周围敌人数量(IBattleChara target, float 施法距离, float 检测范围)
		{
			if (target == null) return 0;

			var player = Svc.ClientState.LocalPlayer;
			if (player == null) return 0;

			// 检查自己与目标的距离是否在施法距离内
			if (Vector3.Distance(player.Position, target.Position) > 施法距离)
				return -1;

			// 获取周围所有敌对目标
			var enemies = Data.AllHostileTargets?
				.Where(e => e != null && 
				            e.IsValid() && 
				            !e.IsDead &&
				            Vector3.Distance(e.Position, target.Position) <= 检测范围)
				.ToList() ?? new List<IBattleChara>();

			// 返回符合条件的敌人数量（不包括我自己和0）
			return enemies.Count(e => e.EntityId != Core.Me.EntityId &&
			                          e.EntityId != 0);
		}
		
		public static int Get目标周围安全敌人数量(IBattleChara target, float 施法距离, float 检测范围)
		{
			if (target == null) return 0;

			var player = Svc.ClientState.LocalPlayer;
			if (player == null) return 0;

			// 检查自己与目标的距离是否在施法距离内
			if (Vector3.Distance(player.Position, target.Position) > 施法距离)
				return -1;
			//不死救赎3039 神圣领域1302 被保护2413 龟壳3054 地天1240
			// 获取周围所有敌对目标
			var enemies = Data.AllHostileTargets?
				.Where(e => e != null &&
				            e.IsValid() &&
				            !e.IsDead &&
			Vector3.Distance(e.Position, target.Position) <= 检测范围)
				.ToList() ?? new List<IBattleChara>();

			if (enemies.Any(e => e.HasAura(1240))) return -1;
			// 返回符合条件的敌人数量（不包括我自己和0）
			return enemies.Count(e => e.EntityId != Core.Me.EntityId &&
			                          e.EntityId != 0);
		}
		

		public static void 通用技能释放(Slot slot, uint skillid, int 距离)
		{
			slot.Add(通用技能释放Check(skillid, 距离));
		}

		public static bool 通用距离检查(int 距离)
		{
			if (PvPSettings.Instance.技能自动选中)
			{
				if (PVPTargetHelper.TargetSelector.Get最近目标().DistanceToPlayer() > 距离 + PvPSettings.Instance.长臂猿 ||
				    PVPTargetHelper.TargetSelector.Get最近目标() == null ||
				    PVPTargetHelper.TargetSelector.Get最近目标() == Core.Me)
				{
					return true;
				}
			}
			else if (!PvPSettings.Instance.技能自动选中 &&
			         Core.Me.GetCurrTarget().DistanceToPlayer() > 距离 + PvPSettings.Instance.长臂猿 ||
			         Core.Me.GetCurrTarget() == Core.Me || Core.Me.GetCurrTarget() == null)
			{
				return true;
			}

			return false;
		}

		public static void 配置(JobViewWindow jobViewWindow)
		{
			通用设置配置();
		}

		public static void 更新日志(JobViewWindow jobViewWindow)
		{
			ImGui.Text("API来自Linto，PVP测试中");
		}
		

		public static void 无目标冲刺()
		{
			if (Core.Me.IsMoving())
			{
				var slot = new Slot();
				if (PvPSettings.Instance.无目标冲刺 && !Core.Me.HasAura(1342u) &&
				    GCDHelper.GetGCDCooldown() == 0 && !check坐骑() && !Core.Me.IsCasting)
				{
					slot.Add(new Spell(29057u, Core.Me));
				}
			}
		}

		private static DateTime _lastMountTime = DateTime.MinValue;

		public static void 无目标坐骑()
		{
			if (!PvPSettings.Instance.无目标坐骑) return;
			if (Core.Me.IsMoving()) return;
			if (GCDHelper.GetGCDCooldown() != 0) return;
			if (check坐骑()) return;
			if (Core.Me.IsCasting) return;
			if (!Core.Me.IsPvP()) return;
			if (Core.Resolve<MemApiMap>().GetCurrTerrId() == 250) return;
			if (TargetHelper.GetNearbyEnemyCount(Core.Me, PvPSettings.Instance.无目标坐骑范围, PvPSettings.Instance.无目标坐骑范围) >
			    1) return;
			DateTime now = DateTime.Now;
			if ((now - _lastMountTime).TotalSeconds < 5)
			{
				return;
			}
			Core.Resolve<MemApiSendMessage>().SendMessage("/ac 随机坐骑");
			_lastMountTime = now;
		}

		public unsafe static void PvP调试窗口()
		{
			if(Svc.ClientState.LocalContentId==19014409517655056)
			{
				ImGui.Begin("调试窗口");
				// ImGui.Text($"gcd:{GCDHelper.GetGCDCooldown()}");
				// ImGui.Text($"万能:{Core.Me.CanAttack()}");
				// ImGui.Text($"最近目标:{PVPTargetHelper.TargetSelector.Get最近目标().Name}");
				// ImGui.Text($"最合适25米目标:{PVPTargetHelper.TargetSelector.Get最合适目标(25).Name}");
				// ImGui.Text($"自己：{Core.Me.Name},{Core.Me.DataId},{Core.Me.Position}");
				// ImGui.Text($"坐骑状态：{Svc.Condition[(ConditionFlag)4]}");
				// ImGui.Text($"血量百分比：{Core.Me.CurrentHpPercent()}");
				// ImGui.Text($"盾值百分比：{Core.Me.ShieldPercentage / 100f}");
				// ImGui.Text($"血量百分比：{Core.Me.CurrentHpPercent() + Core.Me.ShieldPercentage / 100f <= 1.0f}");
				// ImGui.Text(
				// 	$"目标：{Core.Me.GetCurrTarget().Name},{Core.Me.GetCurrTarget().DataId},{Core.Me.GetCurrTarget().Position}");
				// ImGui.Text($"是否移动：{MoveHelper.IsMoving()}");
				// ImGui.Text($"小队人数：{PartyHelper.CastableParty.Count}");
				// ImGui.Text($"25米内敌方人数：{TargetHelper.GetNearbyEnemyCount(Core.Me, 25, 25)}");
				// ImGui.Text($"20米内小队人数：{PartyHelper.CastableAlliesWithin20.Count}");
				// ImGui.Text($"目标5米内人数：{TargetHelper.GetNearbyEnemyCount(Core.Me.GetCurrTarget(), 25, 5)}");
				// ImGui.Text($"LB槽当前数值：{Core.Me.LimitBreakCurrentValue()}");
				// ImGui.Text($"上个技能：{Core.Resolve<MemApiSpellCastSuccess>().LastSpell}");
				// ImGui.Text($"上个GCD：{Core.Resolve<MemApiSpellCastSuccess>().LastGcd}");
				// ImGui.Text($"上个能力技：{Core.Resolve<MemApiSpellCastSuccess>().LastAbility}");
				// ImGui.Text($"上个连击技能：{Core.Resolve<MemApiSpell>().GetLastComboSpellId()})");
				ActionManager* actionManager = ActionManager.Instance();
				uint status = actionManager->GetActionStatus(ActionType.PvPAction, 29230);//GCD状态（本地）
				bool status1 = actionManager->IsActionTargetInRange(ActionType.PetAction,29223);
				bool status2 = ActionManager.CanUseActionOnTarget(29223, (GameObject*)Core.Me.GetCurrTarget().Address);
				ImGui.Text($"状态码：{ActionManager.GetActionInRangeOrLoS(29223,(GameObject*)Svc.ClientState.LocalPlayer.Address,(GameObject*)Core.Me.GetCurrTarget().Address)}");
				ImGui.Text($"test:{status}");
				ImGui.End();
			}
			else
			{
				ImGui.Text("你不需要用到调试");
			}
		}
	}




