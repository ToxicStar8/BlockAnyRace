using Dalamud.Configuration;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface.Colors;
using Dalamud.Memory;
using Dalamud.Plugin;
using ECommons.DalamudServices;
using ImGuiNET;
using System;
using System.Collections.Generic;

namespace Main
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;

        //是否登录就显示窗口
        public bool IsLoginedOpenWindow { get; set; } = true;
        //是否使用Esc可以关闭窗口
        public bool IsEscCloseWindow { get; set; } = true;
        //是否右键添加一个快捷方式
        public bool IsRightClickAddShortcut { get; set; } = true;
        //检测范围
        public int CheckRange { get; set; } = 2;

        //检查时间（毫秒）
        public int CheckMillisecond { get; set; } = 200;

        //屏蔽的种族性别
        public Dictionary<byte, RaceInfo> ByteToRace { get; private set; }

        //屏蔽的指定玩家 Key=CID Value=PlayerInfo
        public Dictionary<ulong, PlayerInfo> BlockTargetRoleDic { get; private set; }

        public void Init()
        {
            BlockTargetRoleDic ??= new();
            ByteToRace ??= new() {
            { 0, new RaceInfo(false,false) },
            { 1, new RaceInfo(false,false) },
            { 2, new RaceInfo(false,false) },
            { 3, new RaceInfo(false,false) },
            { 4, new RaceInfo(false,false) },
            { 5, new RaceInfo(false,false) },
            { 6, new RaceInfo(false,false) },
            { 7, new RaceInfo(false,false) },
            { 8, new RaceInfo(false,false) }};
        }

        public void Save()
        {
            Plugin.Instance.PluginInterface!.SavePluginConfig(this);
        }
    }
}
