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
using ECommons;
using ECommons.Automation.LegacyTaskManager;
using ECommons.DalamudServices;
using ECommons.ExcelServices.TerritoryEnumeration;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;

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
        private readonly string InfoProxyBlackListUpdateSig = "48 89 5C 24 ?? 4C 8B 91 ?? ?? ?? ?? 33 C0";
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

                    var dc = Svc.ClientState.LocalPlayer.CurrentWorld.Value.DataCenter.RowId;
                    var list = Svc.Data.GetExcelSheet<World>().Where(x => x.DataCenter.RowId == dc).ToList();
                    foreach (var item in list)
                    {
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
        private readonly int CheckSecond = 1;
    }
}
