using System;
using Dalamud.Plugin.Services;
using ECommons.Logging;
using ImGuiNET;
using TargetSelector.Windows;

public class VFXHelper : IDisposable
{
    private IFramework Framework { get; init; } = null!;
    private IGameGui GameGui { get; init; } = null!;
    private Core core;

    public VFXHelper(IFramework framework, IGameGui gameGui, Core core)
    {
        this.core = core;
        this.Framework = framework;
        this.GameGui = gameGui;
        PluginInterface.UiBuilder.Draw += OnDrawUI;
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= OnDrawUI;
    }

    public void OnDrawUI()
    {
        var target = core.CurrTarget(core.Me);

        if (target != null)
        {
            var worldPos = target.Position;

            if (this.GameGui.WorldToScreen(worldPos, out var screenPos))
            {
                var drawList = ImGui.GetBackgroundDrawList(); // 使用 GetBackgroundDrawList()
                drawList.AddCircleFilled(screenPos, 30, 0xFF0000FF, 32);

                var text = $"({worldPos.X:F1}, {worldPos.Y:F1}, {worldPos.Z:F1})";
                var textSize = ImGui.CalcTextSize(text);
                PluginLog.Debug($"绘制特效：坐标={screenPos}, 半径=30, 颜色=0xFF0000FF");
                drawList.AddText(screenPos - textSize / 2, 0xFFFFFFFF, text);
            }
        }
    }
}
