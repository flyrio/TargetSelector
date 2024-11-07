using System;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using System.Numerics;
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
using ImGuiNET;
using TargetSelector.Windows;
using ClassJob = Lumina.Excel.GeneratedSheets.ClassJob;

namespace TargetSelector;

public sealed class Plugin : IDalamudPlugin
{
    // 使用PluginService属性注入Dalamud服务
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IFramework CommandManagerFramework { get; private set; } = null!;
    [PluginService] internal static IPluginLog PluginLog { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IToastGui ToastGui { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static ITargetManager TargetManager { get; private set; } = null!;
    private readonly VFXHelper vfxHelper;
    public readonly Core core;
    // 定义插件命令
    private const string CommandName = "/pvp";
    private const string PvpTargetCommandName = "/pvprange";              // 宏命令名称
    private int originalDistance = -1;                                    // 保存原始攻击距离的变量，初始化为-1表示未保存
    private const string PvpTargetMarkerCommandName = "/pvptargetmarker"; // 宏命令名称
    private const string PvpTargetModeCommandName = "/pvptargetmode";     // 新的宏命令名称
    private const string ToggleSelectorCommand = "/pvptoggleselector";
    private const string ToggleNoSwitchTargetCommand = "/pvptogglenoswitch";
    private const string TogglePrioritizeMarkerCommand = "/pvptogglemarker";
    private readonly PVPTargetSelector pvpTargetSelector; //  记住这一行
    public static bool vfxHelperEnabled = true;          // 默认启用
    private const string ToggleVFXHelperCommand = "/pvptogglevfx";
    private const string SetDistanceCommand = "/pvpsetdistance";
    // 插件配置
    public Configuration Configuration { get; init; }
    // 窗口系统
    public readonly WindowSystem WindowSystem = new("TargetSelector");
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }

    private void UpdateTarget(IFramework framework)
    {
        if (!Whitelist.IsWhitelistedUser())
            return;
        
        if (!this.Configuration.选择器开关)
        {
            return;
        }

        

        //PluginLog.Debug($"{Svc.ClientState.IsPvP}");
        var target = this.pvpTargetSelector.GetTarget();
        //PluginLog.Debug($"GetTarget 返回值: {(target == null ? "null" : target.Name)}"); //  添加这行日志
        if (target != null)                                                           // 添加此空检查
        {
            //PluginLog.Debug($"设置目标为：{target.Name}"); // 记录目标名称以进行调试
            this.core.SetTarget(target);
        }
        else
        {
            //PluginLog.Debug("目标为空，不设置目标。"); // 记录目标为空的时间
        }
    }
    public Plugin()
    {
        
        Svc.Init(PluginInterface);                                //  这行代码应该在构造函数的最开始
        this.core = new Core(Svc.ClientState);

        this.pvpTargetSelector = new PVPTargetSelector(this.core, this); // 初始化 pvpTargetSelector
        // 加载或创建配置
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        this.Configuration.Load(); // Load configuration after initialization
        this.vfxHelper = new VFXHelper(Framework, GameGui, core);        // 初始化 VFXHelper，传入 Framework 和 GameGui
        
        // 创建窗口
        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this);

        // 将窗口添加到窗口系统
        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);

        // 添加命令处理器
        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "插件窗口"
        });
        CommandManager.AddHandler(PvpTargetCommandName, new CommandInfo(OnPvpTargetCommand)
        {
            HelpMessage = "一键切换攻击距离到最大，再次使用切换回来" // 添加帮助信息
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

        // 注册UI绘制事件
        PluginInterface.UiBuilder.Draw += DrawUI;
        // 添加打开配置UI的按钮到插件安装器条目
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;

        // 添加打开主UI的按钮
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;
        CommandManagerFramework.Update += UpdateTarget; //  使用 CommandManagerFramework.Update
        Framework.Update += UpdateTarget;               // 使用 Framework.Update，放在构造函数的末尾
        

    }
    


    public void Dispose()
    {
        CommandManagerFramework.Update -= UpdateTarget; //  使用 CommandManagerFramework.Update
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
        this.vfxHelper.Dispose(); // 释放 VFXHelper
        WindowSystem.RemoveAllWindows();
        ConfigWindow.Dispose();
        MainWindow.Dispose();

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
        {
            return;
        }

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
        {
            return;
        }

        string modeName = args.Trim();
        if (Enum.TryParse(modeName, out MainWindow.TargetSelectMode mode))
        {
            Configuration.选择模式 = mode;
            Configuration.Save();
            ToastGui.ShowQuest($"PVP目标模式切换到{mode}"); // 显示toast提示
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
    public void OnDrawUI()
    {
        if (!vfxHelperEnabled) // 通过 PluginInterface 访问 Plugin 实例
        {
            return; // 如果禁用，则直接返回
        }
        var target = TargetManager.Target;

        //if(!core.IsPvP()) return;
        if (target != null && (target.ObjectKind == ObjectKind.Player || target.ObjectKind == ObjectKind.BattleNpc))
        {
            if (target is IBattleChara battleChara)
            {
                var worldPos = target.Position; // 获取目标的世界坐标

                string classJobName = "";
                if (battleChara.ClassJob != null)
                {
                    classJobName = battleChara.ClassJob.GameData.Name;
                }

                var drawList = ImGui.GetBackgroundDrawList();

                if (GameGui.WorldToScreen(worldPos, out var screenPos))
                {
                    if (!string.IsNullOrEmpty(classJobName))
                    {
                        // 设置一个较大的显示尺寸
                        float displaySize = 32;   // 显示大小
                        float displaySize1 = 128; // 显示大小
                        Vector2 iconPos = new Vector2(screenPos.X - displaySize1 / 2, screenPos.Y - displaySize / 2);

                        // 绘制测试方块
                        drawList.AddRectFilled(
                            iconPos,
                            iconPos + new Vector2(displaySize1, displaySize),
                            0x80FF0000 // 半透明红色
                        );

                        // 添加边框
                        drawList.AddRect(
                                iconPos,
                                iconPos + new Vector2(displaySize1, displaySize),
                                0xFFFFFF00, // 黄色边框
                                0.0f,       // 圆角
                                ImDrawFlags.None,
                                2.0f // 边框粗细);
                            );
                            var text = $"{classJobName}";
                            var textSize = ImGui.CalcTextSize(text);
                            drawList.AddText(screenPos - textSize / 2 + new Vector2(0, 0), 0xFFFFFFFF, text);
                    }
                }
            }
        }
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
}
