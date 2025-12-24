using Dalamud.Game;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Hooking;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Logging;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using ECommons;
using ECommons.Automation.LegacyTaskManager;
using ECommons.DalamudServices;
using ECommons.ExcelServices.TerritoryEnumeration;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using World = Lumina.Excel.Sheets.World;

namespace Main
{
    public unsafe partial class Plugin
    {
        [PluginService] public IDalamudPluginInterface PluginInterface { get; private set; } = null!;
        //基础
        public string Name => "BlockAnyRace";
        //指令名
        private const string _commonName = "/bar";
        //窗口事件
        private WindowSystem _windowSystem = new("BlockAnyRace");
        //主UI
        private MainWindow _mainWindow { get; init; }
        //配置
        public Configuration Configuration { get; init; }
        //自己
        public static Plugin Instance;
        //信息栏
        private IDtrBarEntry _dtrEntry;
        //黑名单更新
        private HashSet<ulong> _blackHashSet { get; set; }
        private readonly string InfoProxyBlackListUpdateSig = "E8 ?? ?? ?? ?? 83 7C 24 ?? ?? 75 ?? E8";
        private delegate void InfoProxyBlackListUpdateDelegate(InfoProxyBlacklist.BlockResult* outBlockResult, ulong accountId, ulong contentId);
        private Hook<InfoProxyBlackListUpdateDelegate>? InfoProxyBlackListUpdateHook;

        //服务器列表
        private Dictionary<uint, World> _worlds;
        /// <summary>
        /// 服务器列表
        /// </summary>
        public Dictionary<uint, World>  Worlds
        {
            get
            {
                if (_worlds == null)
                {
                    _worlds = new();

                    var list = Svc.Data.GetExcelSheet<World>();
                    foreach (var item in list)
                    {
                        var name = item.Name.ToString();
                        if (name.IsNullOrWhitespace())
                            continue;
                        Svc.Log.Debug("已添加区服=" + item.Name.ToString());
                        _worlds[item.RowId] = item;
                    }
                }
                return _worlds;
            }
        }

        [NonSerialized]
        private readonly HashSet<ushort> TerritoryTypeWhitelist = [];

        //Update限制
        private DateTime _lastUpdateTime;

        //最后检测到的屏蔽玩家人数
        private int _lastBlockNum;
    }
}
