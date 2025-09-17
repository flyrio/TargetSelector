using System;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Gui.PartyFinder.Types;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.Logging;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using Linto.LintoPvP.PVPApi.PVPApi.Data;
using Lumina.Text.ReadOnly;
using TargetSelector.Windows;

namespace TargetSelector;

public sealed class Plugin : IDalamudPlugin
{
    // 服务注入
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IFramework CommandManagerFramework { get; private set; } = null!;
    [PluginService] internal static IPluginLog PluginLog { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IToastGui ToastGui { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static ITargetManager TargetManager { get; private set; } = null!;
    
    // 命令常量
    private const string CommandName = "/pvp";
    private const string PvpTargetCommandName = "/pvprange";
    private const string PvpTargetMarkerCommandName = "/pvptargetmarker";
    private const string PvpTargetModeCommandName = "/pvptargetmode";
    private const string ToggleSelectorCommand = "/pvptoggleselector";
    private const string ToggleNoSwitchTargetCommand = "/pvptogglenoswitch";
    private const string TogglePrioritizeMarkerCommand = "/pvptogglemarker";
    private const string ToggleVFXHelperCommand = "/pvptogglevfx";
    private const string SetDistanceCommand = "/pvpsetdistance";
    
    // 实例变量
    private readonly VFXHelper vfxHelper;
    public readonly Core core;
    private readonly PVPTargetSelector pvpTargetSelector;
    private int originalDistance = -1;
    
    // 静态配置
    public static bool vfxHelperEnabled = true;
    public static bool targetvfx = true;
    public static bool focustargetvfx = true;
    
    // 配置与UI
    public Configuration Configuration { get; init; }
    public readonly WindowSystem WindowSystem = new("TargetSelector");
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }

    public Plugin()
    {
        Svc.Init(PluginInterface);
        this.core = new Core(Svc.ClientState);

        // 初始化配置
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Load();
        
        // 初始化组件
        this.pvpTargetSelector = new PVPTargetSelector(this.core, this);
        this.vfxHelper = new VFXHelper(Framework, GameGui, core);
        
        // 初始化窗口
        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this);
        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);

        // 注册命令
        RegisterCommands();
        
        // 注册事件
        PluginInterface.UiBuilder.Draw += DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;
        CommandManagerFramework.Update += UpdateTarget;
        Framework.Update += UpdateTarget;
    }

    private void RegisterCommands()
    {
        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "插件窗口"
        });
        CommandManager.AddHandler(PvpTargetCommandName, new CommandInfo(OnPvpTargetCommand)
        {
            HelpMessage = "一键切换攻击距离到最大，再次使用切换回来"
        });
        CommandManager.AddHandler(PvpTargetMarkerCommandName, new CommandInfo(OnPvpTargetMarkerCommand)
        {
            HelpMessage = "切换头标选择,用法：/pvptargetmarker 攻击1"
        });
        CommandManager.AddHandler(PvpTargetModeCommandName, new CommandInfo(OnPvpTargetModeCommand)
        {
            HelpMessage = "切换目标模式,用法: /pvptargetmode 血量最少"
        });
        CommandManager.AddHandler(ToggleSelectorCommand, new CommandInfo(OnToggleSelectorCommand)
        {
            HelpMessage = "启用目标选择器总开关"
        });
        CommandManager.AddHandler(ToggleNoSwitchTargetCommand, new CommandInfo(OnToggleNoSwitchTargetCommand)
        {
            HelpMessage = "不切换目标开关"
        });
        CommandManager.AddHandler(TogglePrioritizeMarkerCommand, new CommandInfo(OnTogglePrioritizeMarkerCommand)
        {
            HelpMessage = "优先锁定标记敌人开关"
        });
        CommandManager.AddHandler(ToggleVFXHelperCommand, new CommandInfo(OnToggleVFXHelperCommand)
        {
            HelpMessage = "切换 VFX Helper 的显示"
        });
        CommandManager.AddHandler(SetDistanceCommand, new CommandInfo(OnSetDistanceCommand)
        {
            HelpMessage = "设置目标选择距离，例如：/pvpsetdistance 25"
        });
    }

private unsafe void UpdateTarget(IFramework framework)
{
    // if (!Whitelist.IsWhitelistedUser())
    //     return;

    // 画家盾逻辑
    if (Configuration.画家盾)
    {
        if (core.HasAura(core.Me, 3202) && 
            ActionManager.Instance()->IsRecastTimerActive(ActionType.Action, 39211) && 
            core.Me.ClassJob.RowId == (uint)Job.PCT)
        {
            ActionManager.Instance()->UseAction(ActionType.Action, 39211, 3758096384, 0, 0, 0, null);
        }
    }
    
    // 选择器开关检查
    if (!this.Configuration.选择器开关)
        return;

    // 获取目标并设置
    var target = this.pvpTargetSelector.GetTarget();
    if (target != null)
    {
        this.core.SetTarget(target);
    }

    // 获取焦点目标并设置
    var focusTarget = pvpTargetSelector.GetFocusTarget();
    if (focusTarget != null)
    {
        core.FocusTarget(focusTarget);
    }
}

private void OnCommand(string command, string args)
{
    // 响应斜杠命令，切换主UI的显示状态
    ToggleMainUI();
}

private void OnPvpTargetCommand(string command, string args)
{
    if (originalDistance == -1) // 如果未保存原始距离
    {
        originalDistance = Configuration.选中距离; // 保存当前距离
        ToastGui.ShowQuest("已切换攻击距离到50米");
        Configuration.选中距离 = 50;
        Configuration.Save();            // 保存配置
    }
    else // 如果已保存原始距离
    {
        Configuration.选中距离 = originalDistance; // 切换回原始距离
        ToastGui.ShowQuest($"已切换攻击距离回{originalDistance}米");
        originalDistance = -1;                 // 重置 originalDistance
        Configuration.Save();                  // 保存配置
    }
}

private void OnPvpTargetMarkerCommand(string command, string args)
{
    if (string.IsNullOrEmpty(args))
        return;

    string markerName = args.Trim();
    if (Enum.TryParse(markerName, out MarkerHelper.HeadMarker marker))
    {
        Configuration.SelectedHeadMarker = marker;
        Configuration.Save();
        ToastGui.ShowQuest($"头标已切换为{marker}");
    }
}

private void OnPvpTargetModeCommand(string command, string args)
{
    if (string.IsNullOrEmpty(args))
        return;

    string modeName = args.Trim();
    if (Enum.TryParse(modeName, out MainWindow.TargetSelectMode mode))
    {
        Configuration.选择模式 = mode;
        Configuration.Save();
        ToastGui.ShowQuest($"PVP目标模式切换到{mode}");
    }
}

private void OnToggleSelectorCommand(string command, string args)
{
    Configuration.选择器开关 = !Configuration.选择器开关;
    Configuration.Save();
    ToastGui.ShowQuest($"目标选择器已{(Configuration.选择器开关 ? "开启" : "关闭")}");
}

private void OnToggleNoSwitchTargetCommand(string command, string args)
{
    Configuration.有目标时不切换目标 = !Configuration.有目标时不切换目标;
    Configuration.Save();
    ToastGui.ShowQuest($"有目标时不切换目标已{(Configuration.有目标时不切换目标 ? "开启" : "关闭")}");
}

private void OnTogglePrioritizeMarkerCommand(string command, string args)
{
    Configuration.头标开关 = !Configuration.头标开关;
    Configuration.Save();
    ToastGui.ShowQuest($"优先选择头标目标已{(Configuration.头标开关 ? "开启" : "关闭")}");
}

private void OnToggleVFXHelperCommand(string command, string args)
{
    vfxHelperEnabled = !vfxHelperEnabled;
    ToastGui.ShowQuest($"VFX已{(vfxHelperEnabled ? "启用" : "禁用")}");
}

private void OnSetDistanceCommand(string command, string args)
{
    if (int.TryParse(args, out int distance) && distance >= 0 && distance <= 50)
    {
        Configuration.选中距离 = distance;
        Configuration.Save();
        ToastGui.ShowQuest($"目标选择距离已设置为 {distance} 米");
    }
    else
    {
        ToastGui.ShowQuest("无效的距离参数，请输入 0 到 50 之间的整数。");
    }
}

public class VFXHelper : IDisposable
{
    private IFramework Framework { get; init; } = null!;
    private IGameGui GameGui { get; init; } = null!;
    private Core core;
    private ISharedImmediateTexture? customIcon;
    private IDalamudTextureWrap? textureWrap;
    private bool textureLoaded = false;

    public VFXHelper(IFramework framework, IGameGui gameGui, Core core)
    {
        this.core = core;
        this.Framework = framework;
        this.GameGui = gameGui;
        PluginInterface.UiBuilder.Draw += OnDrawUI;
    }
    
    private void DrawTargetUI(IGameObject target, bool isFocusTarget)
    {
        if (target == null || !(target.ObjectKind == ObjectKind.Player || target.ObjectKind == ObjectKind.BattleNpc))
            return;

        if ((isFocusTarget && !focustargetvfx) || (!isFocusTarget && !targetvfx))
            return;

        if (target is not IBattleChara battleChara)
            return;

        var worldPos = target.Position;
        string classJobName = battleChara.ClassJob.Value.Name.ToString() ?? "";

        if (string.IsNullOrEmpty(classJobName))
            return;

        var drawList = ImGui.GetBackgroundDrawList();

        if (GameGui.WorldToScreen(worldPos, out var screenPos))
        {
            float displaySize = 32;
            float displaySize1 = 128;
            Vector2 iconPos = new Vector2(screenPos.X - displaySize1 / 2, screenPos.Y - displaySize / 2);

            uint fillColor = isFocusTarget ? 0x8000FF00 : 0x80FF0000; // 绿或红
            drawList.AddRectFilled(iconPos, iconPos + new Vector2(displaySize1, displaySize), fillColor);
            drawList.AddRect(iconPos, iconPos + new Vector2(displaySize1, displaySize), 0xFFFFFF00, 0.0f, ImDrawFlags.None, 2.0f);

            var text = $"{classJobName}  {core.DistanceToPlayerOne(target)}";
            var textSize = ImGui.CalcTextSize(text);
            drawList.AddText(screenPos - textSize / 2 + new Vector2(0, 0), 0xFFFFFFFF, text);
        }
    }
    
    public void OnDrawUI()
    {
        if (!vfxHelperEnabled || !core.IsPvP()) 
        {
            return; // 如果禁用或非PVP，则直接返回
        }

        var target = TargetManager.Target;
        var focustarget = TargetManager.FocusTarget;

        DrawTargetUI(target, false);
        DrawTargetUI(focustarget, true);
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= OnDrawUI;
        textureWrap?.Dispose();
    }
}
    
// 绘制UI
private void DrawUI() => WindowSystem.Draw();

// 切换配置UI的显示状态
public void ToggleConfigUI() => ConfigWindow.Toggle();

// 切换主UI的显示状态
public void ToggleMainUI() => MainWindow.Toggle();
    
public void Dispose()
{
    CommandManagerFramework.Update -= UpdateTarget;
    Framework.Update -= UpdateTarget;
        
    // 移除所有命令处理器
    CommandManager.RemoveHandler(PvpTargetCommandName);
    CommandManager.RemoveHandler(PvpTargetMarkerCommandName);
    CommandManager.RemoveHandler(PvpTargetModeCommandName);
    CommandManager.RemoveHandler(CommandName);
    CommandManager.RemoveHandler(ToggleSelectorCommand);
    CommandManager.RemoveHandler(ToggleNoSwitchTargetCommand);
    CommandManager.RemoveHandler(TogglePrioritizeMarkerCommand);
    CommandManager.RemoveHandler(ToggleVFXHelperCommand);
    CommandManager.RemoveHandler(SetDistanceCommand);
        
    // 释放资源
    this.vfxHelper.Dispose();
    WindowSystem.RemoveAllWindows();
    ConfigWindow.Dispose();
    MainWindow.Dispose();
}
}
