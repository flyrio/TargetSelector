using System;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface.Internal;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using ECommons.Logging;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace TargetSelector.Windows;

public class MainWindow : Window, IDisposable
{
    // 山羊图片路径
    //private string GoatImagePath;

    // 插件实例
    private Plugin Plugin;
    
    public enum TargetSelectMode
    {
        最近单位,
        最远单位,
        血量最少
    }

    // 我们给这个窗口一个隐藏的ID使用##
    // 这样用户会看到"My Amazing Window"作为窗口标题，
    // 但对ImGui来说，ID是"My Amazing Window##With a hidden ID"
    public MainWindow(Plugin plugin)
        : base("PVP目标选择器", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        // 设置窗口大小约束
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(375, 330),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        // 初始化成员变量
        //GoatImagePath = goatImagePath;
        Plugin = plugin;
        Plugin.Configuration.InitializeMarkers();
    }

    // 实现IDisposable接口的方法
    public void Dispose() { }

    // 绘制窗口内容的方法
    public override void Draw()
    {
        ImGui.Checkbox("启用目标选择器", ref Plugin.Configuration.选择器开关);
            int tempDistance = Plugin.Configuration.选中距离;
        ImGui.Checkbox("选中特效开关", ref Plugin.vfxHelperEnabled);
        if (Plugin.vfxHelperEnabled)
        {
            ImGui.Checkbox("当前目标选中特效", ref Plugin.targetvfx);
            ImGui.Checkbox("焦点目标选中特效", ref Plugin.focustargetvfx);
        }
        if (ImGui.SliderInt("选取目标距离", ref tempDistance, 0, 50, "%d"))
        {
            Plugin.Configuration.选中距离 = tempDistance;
            Plugin.Configuration.Save(); // 保存更改
        }
        ImGui.Checkbox("有非自己目标时不切换目标", ref Plugin.Configuration.有目标时不切换目标);
        // ImGui.SameLine();
        // ImGui.Checkbox("选中友方单位时不切换目标", ref Plugin.Configuration.选中友方单位时不切换目标);

        ImGui.Checkbox("自动将最佳AOE目标设置为焦点目标", ref Plugin.Configuration.最佳AOE目标);
        if (Plugin.Configuration.最佳AOE目标)
        {
            ImGui.SliderInt("目标及其周围多大半径（通常为圆形AOE伤害范围)", ref Plugin.Configuration.AOE技能伤害范围, 1, 10);
            ImGui.SliderInt("最低目标数量", ref Plugin.Configuration.AOE数量, 1, 10);
        }
        
        ImGui.Checkbox("优先选中头标敌人", ref Plugin.Configuration.头标开关);
        if (Plugin.Configuration.头标开关)
        {
            // 获取所有 HeadMarker 枚举值
            var headMarkers = Enum.GetValues(typeof(MarkerHelper.HeadMarker)).Cast<MarkerHelper.HeadMarker>().ToArray();

            // 获取当前选择的 HeadMarker
            var currentMarker = Plugin.Configuration.SelectedHeadMarker;

            // 创建下拉框
            if (ImGui.BeginCombo("选择头标", currentMarker.ToString()))
            {
                foreach (var marker in headMarkers)
                {
                    bool isSelected = marker == currentMarker;
                    if (ImGui.Selectable(marker.ToString(), ref isSelected))
                    {
                        Plugin.Configuration.SelectedHeadMarker = marker; // 更新选择的 HeadMarker
                        Plugin.Configuration.Save();                      // 保存配置
                    }
                    if (isSelected)
                    {
                        ImGui.SetItemDefaultFocus();
                    }
                }
                ImGui.EndCombo();
            }
        }
        
        if (ImGui.BeginCombo("选择模式", Plugin.Configuration.选择模式.ToString()))
        {
            foreach (var mode in Enum.GetValues(typeof(TargetSelectMode)))
            {
                bool isSelected = (TargetSelectMode)mode == Plugin.Configuration.选择模式;
                if (ImGui.Selectable(mode.ToString(), ref isSelected))
                {
                    Plugin.Configuration.选择模式 = (TargetSelectMode)mode;
                }
                if (isSelected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }
            ImGui.EndCombo();
        }
        ImGui.Checkbox("排除被保护目标", ref Plugin.Configuration.排除被保护目标);
        ImGui.SameLine();
        ImGui.Checkbox("排除骑士无敌", ref Plugin.Configuration.排除骑士无敌);
        ImGui.SameLine();
        ImGui.Checkbox("排除黑骑无敌", ref Plugin.Configuration.排除黑骑无敌);
        ImGui.Checkbox("排除地天", ref Plugin.Configuration.排除地天);
        ImGui.SameLine();
        ImGui.Checkbox("排除龟壳", ref Plugin.Configuration.排除龟壳);
        
        ImGui.Checkbox("画家盾防地天", ref Plugin.Configuration.画家盾);
    }
}
    

