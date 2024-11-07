using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lumina.Excel.GeneratedSheets2;
using TargetSelector.Windows;

namespace TargetSelector;

// 标记为可序列化
[Serializable]
public class Configuration : IPluginConfiguration
{

    public int Version { get; set; } = 1;

    public bool IsConfigWindowMovable { get; set; } = true;
    public bool SomePropertyToBeSavedAndWithADefault { get; set; } = true;

    public bool 选择器开关 = false;
    public int 选中距离 = 50;
    public bool 限制PVP = true;
    public bool 头标开关 = true;
    public bool 选中友方单位时不切换目标 = false;
    public bool 有目标时不切换目标 = false;
    
    public bool 排除被保护目标 = true;
    public bool 排除骑士无敌 = true;
    public bool 排除黑骑无敌 = true;
    public bool 排除地天 = true;
    public bool 排除龟壳 = true;
    public MainWindow.TargetSelectMode 选择模式 { get; set; } = MainWindow.TargetSelectMode.最近单位;
    
    public Dictionary<MarkerHelper.HeadMarker, bool> MarkerEnabled { get; set; } = new();
    public List<MarkerHelper.HeadMarker> MarkerPriority { get; set; } = new();
    public MarkerHelper.HeadMarker SelectedHeadMarker { get; set; } = MarkerHelper.HeadMarker.攻击1;

    public void InitializeMarkers()
    {
        foreach (MarkerHelper.HeadMarker marker in Enum.GetValues(typeof(MarkerHelper.HeadMarker)))
        {
            if (!MarkerEnabled.ContainsKey(marker))
            {
                MarkerEnabled[marker] = true;
            }

            if (!MarkerPriority.Contains(marker))
            {
                MarkerPriority.Add(marker);
            }
        }
    }
    
    public void Load()
    {
        MarkerPriority = Enum.GetValues(typeof(MarkerHelper.HeadMarker)).Cast<MarkerHelper.HeadMarker>().ToList(); // Initialize with all markers
        if (!MarkerPriority.Contains(SelectedHeadMarker))
        {
            MarkerPriority.Insert(0, SelectedHeadMarker);
        }
        else
        {
            MarkerPriority.Remove(SelectedHeadMarker);
            MarkerPriority.Insert(0, SelectedHeadMarker);
        }
        if (MarkerEnabled.Count == 0)
        {
            InitializeMarkers();
        }
        MarkerPriority = MarkerPriority.Distinct().ToList();
    }

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
    
}
