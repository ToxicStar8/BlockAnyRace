using Dalamud.Game;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Network;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Hooking;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Logging;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using ECommons;
using ECommons.Automation;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Common.Lua;
using Lumina.Data.Parsing.Layer;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Formats.Tar;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Numerics;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace Main
{
    /// <summary>
    /// 插件入口
    /// </summary>
    public unsafe partial class Plugin : IDalamudPlugin
    {
        //构造函数
        public Plugin(IDalamudPluginInterface pluginInterface)
        {
            PluginInterface = pluginInterface;
            //初始化
            Instance = this;
            ECommonsMain.Init(PluginInterface, this);

            //new配置出来
            Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Configuration.Init();
            //窗口类
            _mainWindow = new MainWindow();
            _windowSystem.AddWindow(_mainWindow);
            //信息栏
            _dtrEntry ??= Svc.DtrBar.Get(Name);
            _dtrEntry.Shown = true;
            _dtrEntry.OnClick += OnClick_Dtr;
            //黑名单
            _blackHashSet ??= new HashSet<ulong>();
            InfoProxyBlackListUpdateHook ??= Svc.Hook.HookFromSignature<InfoProxyBlackListUpdateDelegate>(InfoProxyBlackListUpdateSig, InfoProxyBlackListUpdateDetour);
            InfoProxyBlackListUpdateHook.Enable();

            //绑定指令监听
            Svc.Commands.AddHandler(_commonName, new CommandInfo(OpenMainUI)
            {
                HelpMessage = Lang.OpenSetting
            });

            //绘制UI
            Svc.PluginInterface.UiBuilder.Draw += DrawUI;
            Svc.PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;
            Svc.ContextMenu.OnMenuOpened += OnOpenContextMenu;

            //Core
            Svc.Framework.Update += Update;

            _mainWindow.RespectCloseHotkey = Configuration.IsEscCloseWindow;

            var tbTerritoryType = Svc.Data.GameData.Excel.GetSheet<TerritoryType>();
            var valueTuples = tbTerritoryType?.Where(
                    //TerritoryIntendedUse=区域预期用途
                    x => x.TerritoryIntendedUse.Value.RowId is 0 or 1 or 13 or 19 or 21 or 23 or 44 or 46 or 47 &&
                          !string.IsNullOrEmpty(x.Name.ToString()) && 
                          x.RowId is not 136).Select(
                x => ((ushort)x.RowId, x.PlaceName.Value.Name.ToString() ?? "Unknown Place"));

            if (valueTuples is not null)
            {
                foreach ((ushort rowId, string placeName) in valueTuples)
                {
                    TerritoryTypeWhitelist.Add(rowId);
                    Svc.Log.Debug("已添加屏蔽的区域=" + placeName);
                }
            }

            //启动时根据情况选择是否开启，方便测试
            _mainWindow.IsOpen = Configuration.IsLoginedOpenWindow;
        }

        #region Black Hook
        private void InfoProxyBlackListUpdateDetour(InfoProxyBlacklist.BlockResult* outBlockResult, ulong accountId, ulong contentId)
        {
            InfoProxyBlackListUpdateHook.Original(outBlockResult, accountId, contentId);

            //触发了黑名单更新
            if (outBlockResult->BlockedCharacterIndex != _blackHashSet.Count)
            {
                ResetBlackList();
            }
        }

        private void ResetBlackList()
        {
            //启动/更新时，统计一次
            var tempHashSet = new HashSet<ulong>();
            var count = InfoProxyBlacklist.Instance()->BlockedCharactersCount;
            foreach (var blockCharacter in InfoProxyBlacklist.Instance()->BlockedCharacters)
            {
                if (blockCharacter.Id is not 0)
                {
                    //blockCharacter.Id = accountId for new, contentId for old
                    tempHashSet.Add(blockCharacter.Id);

                    //BlockedCharacters只增不减，必须使用BlockedCharactersCount处理变化后的数量
                    if (tempHashSet.Count >= count)
                    {
                        break;
                    }
                }
            }
            _blackHashSet = tempHashSet;
        }
        #endregion

        #region Core
        private void Update(IFramework framework)
        {
            if ((DateTime.Now - _lastUpdateTime).TotalMilliseconds < Configuration.CheckMillisecond) return;
            if (!TerritoryTypeWhitelist.Contains(Svc.ClientState.TerritoryType)) return;
            if (_dtrEntry is null) return;
            if (Svc.Objects.LocalPlayer is not { } localPlayer) return;

            var blockNum = 0;
            var sb = new StringBuilder();

            var myPos = localPlayer.Position;
            var checkRange = Configuration.CheckRange * Configuration.CheckRange;

            var invisible = VisibilityFlags.Model;

            foreach (var obj in Svc.Objects)
            {
                if (obj is { ObjectKind: Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Player })
                {
                    var chara = (Character*)obj.Address;
                    //转不出来
                    if (chara is null)
                    {
                        continue;
                    }

                    //是自己
                    if (chara->ContentId == Svc.PlayerState.ContentId)
                    {
                        continue;
                    }

                    //转不出服务器也拦截，因为跨大区
                    if (!Worlds.TryGetValue(chara->HomeWorld,out var world))
                    {
                        //Svc.Log.Debug(chara->NameString + " 跨区HomeWorld=" + chara->HomeWorld);
                        //continue;
                    }

                    //好友
                    if (!Configuration.IsBlockFriend && chara->IsFriend)
                    {
                        continue;
                    }

                    //小队里
                    if (!Configuration.IsBlockParty && chara->IsPartyMember)
                    {
                        continue;
                    }

                    var race = chara->DrawData.CustomizeData.Race;
                    if (!Configuration.ByteToRace.TryGetValue(race ,out var raceInfo))
                    {
                        continue;
                    }

                    //Svc.Log.Debug(chara->NameString + " RenderFlags=" + chara->RenderFlags + " Race=" + race);

                    var needDeletePos = obj.Position;
                    //屏蔽特定种族与性别
                    if ((raceInfo.IsHideMale && chara->Sex is 0) || (raceInfo.IsHideFemale && chara->Sex is 1))
                    {
                        //与为0，则隐藏
                        if ((chara->RenderFlags & invisible) is 0)
                        {
                            chara->RenderFlags |= invisible;
                        }
                        //不进行根号计算，消耗大
                        if (Vector3.DistanceSquared(myPos, needDeletePos) <= checkRange)
                        {
                            var sex = chara->Sex;
                            if(sex is 0 or 1)
                            {
                                var sexStr = Lang.Sex[sex];
                                var str = $"{obj.Name} @{world.Name} @{Lang.RaceName[race]} {sexStr}";
                                sb.AppendLine(str);
                                blockNum++;
                            }
                        }
                    }
                    //非指定种族情况，需要判定黑名单和指定玩家
                    else
                    {
                        bool isBlack = _blackHashSet.Contains(chara->ContentId) || _blackHashSet.Contains(chara->AccountId);
                        bool isTarget = Configuration.BlockTargetRoleDic.ContainsKey(chara->ContentId);
                        if (isBlack || isTarget)
                        {
                            //如果是指定玩家，屏蔽
                            if (isTarget)
                            {
                                if ((chara->RenderFlags & invisible) is 0)
                                {
                                    chara->RenderFlags |= invisible;
                                }
                            }
                            //不进行根号计算，消耗大
                            if (Vector3.DistanceSquared(myPos, needDeletePos) <= checkRange)
                            {
                                var str = isTarget ? Lang.TargetRole : Lang.BlackList;
                                sb.AppendLine($"{obj.Name} @{world.Name} @{str}");
                                blockNum++;
                            }
                            continue;
                        }
                        
                        //非屏蔽玩家且被屏蔽（隐身？），改为默认值
                        if ((chara->RenderFlags & invisible) is not 0)
                        {
                            chara->RenderFlags = VisibilityFlags.None;
                        }
                    }
                }
            }

            _dtrEntry.Text = string.Format(Lang.BlockNum, blockNum.ToString());
            _dtrEntry.Tooltip = sb.ToString().Trim();

            //
            _lastUpdateTime = DateTime.Now;

            //通知
            if(Configuration.IsShowTipsInChat && blockNum > _lastBlockNum)
            {
                //发送聊天通知
                Chat.SendMessage("/e " + Configuration.EchoTips);
            }
            _lastBlockNum = blockNum;
        }
        #endregion

        #region Common
        private void OnClick_Dtr(DtrInteractionEvent e)
        {
            _mainWindow.Toggle();
        }

        //绘制UI方法
        private void DrawUI()
        {
            _windowSystem.Draw();
        }

        /// <summary>
        /// 打开主UI
        /// </summary>
        private void OpenMainUI(string command, string args)
        {
            _mainWindow.Toggle();
        }

        private void ToggleMainUI()
        {
            _mainWindow.Toggle();
        }
        #endregion

        #region Right Click Menu
        private void OnOpenContextMenu(IMenuOpenedArgs menuOpenedArgs)
        {
            if (menuOpenedArgs.Target is not MenuTargetDefault menuTargetDefault)
            {
                return;
            }
            if (!IsMenuValid(menuOpenedArgs))
            {
                return;
            }
            if (!Configuration.IsRightClickAddShortcut)
            {
                return;
            }

            var cid = menuTargetDefault.TargetContentId;
            if (Configuration.BlockTargetRoleDic.ContainsKey(cid))
            {
                menuOpenedArgs.AddMenuItem(new MenuItem
                {
                    PrefixChar = 'B',
                    PrefixColor = 1,
                    Name = Lang.RemoveBlockList,
                    OnClicked = RemoveRoleToBlockDic
                });
            }
            else
            {
                menuOpenedArgs.AddMenuItem(new MenuItem
                {
                    PrefixChar = 'B',
                    PrefixColor = 1,
                    Name = Lang.AddBlockList,
                    OnClicked = AddRoleToBlockDic
                });
            }
        }

        private bool IsMenuValid(IMenuArgs menuOpenedArgs)
        {
            if (menuOpenedArgs.Target is not MenuTargetDefault menuTargetDefault)
            {
                return false;
            }
            switch (menuOpenedArgs.AddonName)
            {
                case null: // Nameplate/Model menu
                case "LookingForGroup":
                case "PartyMemberList":
                case "FriendList":
                case "FreeCompany":
                case "SocialList":
                case "ContactList":
                case "ChatLog":
                case "_PartyList":
                case "LinkShell":
                case "CrossWorldLinkshell":
                case "ContentMemberList": // Eureka/Bozja/...
                case "BeginnerChatList":
                    return menuTargetDefault.TargetName is  not null && menuTargetDefault.TargetHomeWorld.Value.RowId is not 0 && menuTargetDefault.TargetHomeWorld.Value.RowId is not 65535;
                case "BlackList":
                    return menuTargetDefault.TargetName != string.Empty;

                default:
                    return false;
            }
        }

        private void RemoveRoleToBlockDic(IMenuItemClickedArgs args)
        {
            if (args.Target is not MenuTargetDefault menuTargetDefault)
            {
                return;
            }
            var cid = menuTargetDefault.TargetContentId;
            Configuration.BlockTargetRoleDic.Remove(cid);
        }

        private void AddRoleToBlockDic(IMenuItemClickedArgs args)
        {
            if (args.Target is not MenuTargetDefault menuTargetDefault)
            {
                return;
            }
            var cid = menuTargetDefault.TargetContentId;
            if (cid is 0)
            {
                Svc.Log.Error("需要添加屏蔽的人员CID为0");
                return;
            }
            Configuration.BlockTargetRoleDic[cid] = new PlayerInfo(cid, menuTargetDefault.TargetName, menuTargetDefault.TargetHomeWorld.Value.Name.ToString());
        }
        #endregion

        //退出方法
        public void Dispose()
        {
            //保存配置
            Configuration.Save();
            //移除窗口监听
            _windowSystem.RemoveAllWindows();
            //关闭窗口
            _mainWindow.Dispose();
            //移除指令监听
            Svc.Commands.RemoveHandler(_commonName);
            //移除绘制监听
            Svc.PluginInterface.UiBuilder.Draw -= DrawUI;
            Svc.PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUI;
            Svc.ContextMenu.OnMenuOpened -= OnOpenContextMenu;
            //移除信息栏
            if(_dtrEntry is { })
            {
                _dtrEntry.OnClick -= OnClick_Dtr;
                _dtrEntry.Remove();
                _dtrEntry = null;
            }
            //移除黑名单Hook
            InfoProxyBlackListUpdateHook?.Disable();
            InfoProxyBlackListUpdateHook = null;
            //移除Update监听
            Svc.Framework.Update -= Update;
            //
            ECommonsMain.Dispose();
        }
    }
}
