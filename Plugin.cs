using System;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Gui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using TargetSelector.Windows;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace TargetSelector;

public sealed class Plugin : IDalamudPlugin
{
    // 服务注入
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
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

        // ⚡ 只注册一次 Update（避免重复跑）
        Framework.Update += UpdateTarget;
    }

    private void RegisterCommands()
    {
        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand) { HelpMessage = "插件窗口" });
        CommandManager.AddHandler(PvpTargetCommandName, new CommandInfo(OnPvpTargetCommand) { HelpMessage = "一键切换攻击距离到最大，再次使用切换回来" });
        CommandManager.AddHandler(PvpTargetMarkerCommandName, new CommandInfo(OnPvpTargetMarkerCommand) { HelpMessage = "切换头标选择,用法：/pvptargetmarker 攻击1" });
        CommandManager.AddHandler(PvpTargetModeCommandName, new CommandInfo(OnPvpTargetModeCommand) { HelpMessage = "切换目标模式,用法: /pvptargetmode 血量最少" });
        CommandManager.AddHandler(ToggleSelectorCommand, new CommandInfo(OnToggleSelectorCommand) { HelpMessage = "启用目标选择器总开关" });
        CommandManager.AddHandler(ToggleNoSwitchTargetCommand, new CommandInfo(OnToggleNoSwitchTargetCommand) { HelpMessage = "不切换目标开关" });
        CommandManager.AddHandler(TogglePrioritizeMarkerCommand, new CommandInfo(OnTogglePrioritizeMarkerCommand) { HelpMessage = "优先锁定标记敌人开关" });
        CommandManager.AddHandler(ToggleVFXHelperCommand, new CommandInfo(OnToggleVFXHelperCommand) { HelpMessage = "切换 VFX Helper 的显示" });
        CommandManager.AddHandler(SetDistanceCommand, new CommandInfo(OnSetDistanceCommand) { HelpMessage = "设置目标选择距离，例如：/pvpsetdistance 25" });
    }

    private unsafe void UpdateTarget(IFramework framework)
    {
        // 画家盾逻辑 (仅在满足条件时执行)
        if (Configuration.画家盾
            && core.Me.ClassJob.RowId == (uint)Job.PCT
            && core.HasAura(core.Me, 3202)
            && ActionManager.Instance()->IsRecastTimerActive(ActionType.Action, 39211))
        {
            ActionManager.Instance()->UseAction(ActionType.Action, 39211, 0xE0000000, 0, 0, 0, null);
        }

        if (!Configuration.选择器开关)
            return;

        // 设置目标
        var target = pvpTargetSelector.GetTarget();
        if (target != null) core.SetTarget(target);

        // 设置焦点目标
        var focusTarget = pvpTargetSelector.GetFocusTarget();
        if (focusTarget != null) core.FocusTarget(focusTarget);
    }

    private void OnCommand(string command, string args) => ToggleMainUI();

    private void OnPvpTargetCommand(string command, string args)
    {
        if (originalDistance == -1)
        {
            originalDistance = Configuration.选中距离;
            ToastGui.ShowQuest("已切换攻击距离到50米");
            Configuration.选中距离 = 50;
        }
        else
        {
            Configuration.选中距离 = originalDistance;
            ToastGui.ShowQuest($"已切换攻击距离回{originalDistance}米");
            originalDistance = -1;
        }
        Configuration.Save();
    }

    private void OnPvpTargetMarkerCommand(string command, string args)
    {
        if (string.IsNullOrEmpty(args)) return;
        if (Enum.TryParse(args.Trim(), out MarkerHelper.HeadMarker marker))
        {
            Configuration.SelectedHeadMarker = marker;
            Configuration.Save();
            ToastGui.ShowQuest($"头标已切换为{marker}");
        }
    }

    private void OnPvpTargetModeCommand(string command, string args)
    {
        if (string.IsNullOrEmpty(args)) return;
        if (Enum.TryParse(args.Trim(), out MainWindow.TargetSelectMode mode))
        {
            Configuration.选择模式 = mode;
            Configuration.Save();
            ToastGui.ShowQuest($"PVP目标模式切换到{mode}");
        }
    }

    private void OnToggleSelectorCommand(string c, string a)
    {
        Configuration.选择器开关 = !Configuration.选择器开关;
        Configuration.Save();
        ToastGui.ShowQuest($"目标选择器已{(Configuration.选择器开关 ? "开启" : "关闭")}");
    }

    private void OnToggleNoSwitchTargetCommand(string c, string a)
    {
        Configuration.有目标时不切换目标 = !Configuration.有目标时不切换目标;
        Configuration.Save();
        ToastGui.ShowQuest($"有目标时不切换目标已{(Configuration.有目标时不切换目标 ? "开启" : "关闭")}");
    }

    private void OnTogglePrioritizeMarkerCommand(string c, string a)
    {
        Configuration.头标开关 = !Configuration.头标开关;
        Configuration.Save();
        ToastGui.ShowQuest($"优先选择头标目标已{(Configuration.头标开关 ? "开启" : "关闭")}");
    }

    private void OnToggleVFXHelperCommand(string c, string a)
    {
        vfxHelperEnabled = !vfxHelperEnabled;
        ToastGui.ShowQuest($"VFX已{(vfxHelperEnabled ? "启用" : "禁用")}");
    }

    private void OnSetDistanceCommand(string c, string args)
    {
        if (int.TryParse(args, out int d) && d >= 0 && d <= 50)
        {
            Configuration.选中距离 = d;
            Configuration.Save();
            ToastGui.ShowQuest($"目标选择距离已设置为 {d} 米");
        }
        else
        {
            ToastGui.ShowQuest("无效的距离参数，请输入 0 到 50 之间的整数。");
        }
    }

    // ---------------- VFX Helper ----------------
    public class VFXHelper : IDisposable
    {
        private readonly Core core;
        private IFramework Framework;
        private IGameGui GameGui;

        public VFXHelper(IFramework framework, IGameGui gameGui, Core core)
        {
            this.core = core;
            this.Framework = framework;
            this.GameGui = gameGui;
            PluginInterface.UiBuilder.Draw += OnDrawUI;
        }
        
        private void DrawTargetUI(IGameObject target, bool isFocusTarget)
        {
            if (target is not IBattleChara bc) return;
            if (!vfxHelperEnabled) return;
            if ((isFocusTarget && !focustargetvfx) || (!isFocusTarget && !targetvfx)) return;

            if (!GameGui.WorldToScreen(target.Position, out var screenPos)) return;
            var drawList = ImGui.GetBackgroundDrawList();

            float width = 128f, height = 32f;
            Vector2 pos = new(screenPos.X - width/2, screenPos.Y - height/2);

            uint fill = isFocusTarget ? 0x8000FF00u : 0x80FF0000u;
            drawList.AddRectFilled(pos, pos + new Vector2(width, height), fill);
            drawList.AddRect(pos, pos + new Vector2(width, height), 0xFFFFFFFF);

            // 避免字符串拼接：分两次渲染
            float distance = core.DistanceToPlayerOne(target);
            drawList.AddText(screenPos + new Vector2(-60, -10), 0xFFFFFFFF, bc.ClassJob.Value.Name.ToString());
            drawList.AddText(screenPos + new Vector2(-10, 10), 0xFFFFFFFF, $"{distance:0.0}m");
        }

        public void OnDrawUI()
        {
            if (!vfxHelperEnabled || !core.IsPvP()) return;
            var target = TargetManager.Target;
            var focus = TargetManager.FocusTarget;
            DrawTargetUI(target, false);
            DrawTargetUI(focus, true);
        }

        public void Dispose() => PluginInterface.UiBuilder.Draw -= OnDrawUI;
    }
    
    private void DrawUI() => WindowSystem.Draw();
    public void ToggleConfigUI() => ConfigWindow.Toggle();
    public void ToggleMainUI() => MainWindow.Toggle();
    
    public void Dispose()
    {
        Framework.Update -= UpdateTarget;
        CommandManager.RemoveHandler(PvpTargetCommandName);
        CommandManager.RemoveHandler(PvpTargetMarkerCommandName);
        CommandManager.RemoveHandler(PvpTargetModeCommandName);
        CommandManager.RemoveHandler(CommandName);
        CommandManager.RemoveHandler(ToggleSelectorCommand);
        CommandManager.RemoveHandler(ToggleNoSwitchTargetCommand);
        CommandManager.RemoveHandler(TogglePrioritizeMarkerCommand);
        CommandManager.RemoveHandler(ToggleVFXHelperCommand);
        CommandManager.RemoveHandler(SetDistanceCommand);

        vfxHelper.Dispose();
        WindowSystem.RemoveAllWindows();
        ConfigWindow.Dispose();
        MainWindow.Dispose();
    }
}
