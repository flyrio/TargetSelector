using System;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface.Windowing;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using ImGuiNET;

namespace TargetSelector.Windows;

public class ConfigWindow : Window, IDisposable
{
    // 配置对象
    private Configuration Configuration;

    // 构造函数
    // 我们给这个窗口一个常量ID使用###
    // 这允许标签是动态的，比如"{FPS Counter}fps###XYZ counter window"，
    // 而窗口ID对ImGui来说始终是"###XYZ counter window"
    public ConfigWindow(Plugin plugin) : base("设置")
    {
        // 设置窗口标志
        Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse;

        // 设置窗口大小
        Size = new Vector2(232, 90);
        SizeCondition = ImGuiCond.Always;

        // 获取配置
        Configuration = plugin.Configuration;
    }

    // 实现IDisposable接口的方法
    public void Dispose() { }

    // 在Draw()调用之前执行的方法
    public override void PreDraw()
    {
        // 标志必须在Draw()被调用之前添加或移除，否则不会生效
        if (Configuration.IsConfigWindowMovable)
        {
            // 如果窗口可移动，移除NoMove标志
            Flags &= ~ImGuiWindowFlags.NoMove;
        }
        else
        {
            // 如果窗口不可移动，添加NoMove标志
            Flags |= ImGuiWindowFlags.NoMove; 
        }
    }

    // 绘制窗口内容的方法
    public override void Draw()
    {
        // // 不能直接引用属性，所以使用局部副本
        // var configValue = Configuration.SomePropertyToBeSavedAndWithADefault;
        // // 创建一个复选框
        // if (ImGui.Checkbox("Random Config Bool", ref configValue))
        // {
        //     // 如果复选框状态改变，更新配置
        //     Configuration.SomePropertyToBeSavedAndWithADefault = configValue;
        //     // 可以在更改时立即保存，如果你不想提供"保存并关闭"按钮
        //     Configuration.Save();
        // }
        //
        // // 创建另一个复选框控制窗口是否可移动
        // var movable = Configuration.IsConfigWindowMovable;
        // if (ImGui.Checkbox("Movable Config Window", ref movable))
        // {
        //     // 如果复选框状态改变，更新配置
        //     Configuration.IsConfigWindowMovable = movable;
        //     Configuration.Save();
        // }
        ImGui.Text($"cid{Svc.ClientState.LocalContentId}");
        if (Whitelist.IsWhitelistedUser())
            ImGui.Text("已授权");
        else
        {
            ImGui.Text("前方的区域以后再来探索吧");
        }

    }
}
