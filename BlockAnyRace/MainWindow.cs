using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Xml.Linq;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface.Internal;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using ECommons.GameFunctions;
using ECommons.ImGuiMethods;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Dalamud.Game.ClientState.Objects.Enums;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using ImGuiScene;
using Dalamud.Interface.Utility.Table;
using System.Data.Common;
using System.Diagnostics;
using Dalamud.Bindings.ImGui;

namespace Main
{
    public unsafe class MainWindow : Window, IDisposable
    {
        public MainWindow() : base(Plugin.Instance.Name, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
        {
            //设置窗口大小
            this.SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(260, 150),
                MaximumSize = new Vector2(600, 600)
            };
        }

        public override void Draw()
        {
            var config = Plugin.Instance.Configuration;

            ImGui.BeginTabBar(Plugin.Instance.Name);

            //选择种族性别
            if (ImGui.BeginTabItem(Lang.SelectBlockRaceTitle))
            {
                ImGui.BeginTable("bar-raceTable", 3, ImGuiTableFlags.NoBordersInBody | ImGuiTableFlags.SizingFixedSame);

                ImGui.TableSetupColumn(Lang.Race);
                ImGui.TableSetupColumn(Lang.Sex[0]);
                ImGui.TableSetupColumn(Lang.Sex[1]);
                ImGui.TableHeadersRow();

                foreach (var item in config.ByteToRace)
                {
                    if (item.Key is 0)
                    {
                        continue;
                    }

                    ImGui.TableNextRow();

                    ImGui.TableSetColumnIndex(0);
                    ImGui.Text(Lang.RaceName[item.Key]);

                    ImGui.TableSetColumnIndex(1);
                    ImGui.PushID(item.Key + 100);
                    bool isHideMale = item.Value.IsHideMale;
                    ImGui.Checkbox("", ref isHideMale);
                    item.Value.IsHideMale = isHideMale;
                    if (item.Value.IsHideMale != isHideMale)
                    {
                        item.Value.IsHideMale = isHideMale;
                        config.Save();
                    }
                    ImGui.PopID();

                    ImGui.TableSetColumnIndex(2);
                    ImGui.PushID(item.Key + 200);
                    bool isHideFemale = item.Value.IsHideFemale;
                    ImGui.Checkbox("", ref isHideFemale);
                    if (item.Value.IsHideFemale != isHideFemale)
                    {
                        item.Value.IsHideFemale = isHideFemale;
                        config.Save();
                    }

                    ImGui.PopID();

                }

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                ImGui.Text("");
                //全选
                ImGui.TableSetColumnIndex(1);
                if (ImGui.Button(Lang.AllSelect))
                {
                    foreach (var item in config.ByteToRace)
                    {
                        item.Value.IsHideFemale = true;
                        item.Value.IsHideMale = true;
                    }
                    config.Save();
                }
                //反选
                ImGui.TableSetColumnIndex(2);
                if (ImGui.Button(Lang.Reverse))
                {
                    foreach (var item in config.ByteToRace)
                    {
                        item.Value.IsHideFemale = !item.Value.IsHideFemale;
                        item.Value.IsHideMale = !item.Value.IsHideMale;
                    }
                    config.Save();
                }

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                ImGui.Text("");
                //保存预设
                ImGui.TableSetColumnIndex(1);
                if (ImGui.Button(Lang.SavePresets))
                {
                    config.TempByteToRace ??= new();
                    foreach (var item in config.ByteToRace)
                    {
                        var key = item.Key;
                        var curInfo = config.ByteToRace[key];
                        config.TempByteToRace[key] = new(curInfo.IsHideMale, curInfo.IsHideFemale);
                    }
                    config.Save();
                }
                //读取预设
                ImGui.TableSetColumnIndex(2);
                if (ImGui.Button(Lang.ReadPresets))
                {
                    if (config.TempByteToRace is null || config.TempByteToRace.Count is 0)
                        return;

                    config.ByteToRace ??= new();
                    foreach (var item in config.TempByteToRace)
                    {
                        var key = item.Key;
                        var tempInfo = config.TempByteToRace[key];
                        config.ByteToRace[key] = new(tempInfo.IsHideMale, tempInfo.IsHideFemale);
                    }
                    config.Save();
                }
                ImGui.EndTable();

                ImGui.EndTabItem();
            }

            //列表
            if (ImGui.BeginTabItem(Lang.TargetBlockList))
            {
                ImGui.BeginTable("bar-targetRoleTable", 3, ImGuiTableFlags.NoBordersInBody | ImGuiTableFlags.SizingFixedSame);

                ImGui.TableSetupColumn(Lang.RoleName);
                ImGui.TableSetupColumn(Lang.WorldServer);
                ImGui.TableSetupColumn("");
                ImGui.TableHeadersRow();

                ulong removeKey = 0;
                foreach (var item in Plugin.Instance.Configuration.BlockTargetRoleDic)
                {
                    ImGui.TableNextRow();

                    var cid = item.Key.ToString();

                    ImGui.PushID(cid);
                    ImGui.TableSetColumnIndex(0);
                    ImGui.Text(item.Value.Name);

                    ImGui.TableSetColumnIndex(1);
                    ImGui.Text(item.Value.HomeWorld);

                    ImGui.TableSetColumnIndex(2);
                    if (ImGui.Button(Lang.Remove))
                    {
                        removeKey = item.Key;
                    }
                    ImGui.PopID();
                }
                if (removeKey is not  0)
                {
                    Plugin.Instance.Configuration.BlockTargetRoleDic.Remove(removeKey);
                    Plugin.Instance.Configuration.Save();
                }
                ImGui.EndTable();

                ImGui.EndTabItem();
            }

            //设置
            if (ImGui.BeginTabItem(Lang.Setting))
            {
                //检查范围
                var checkRange = Plugin.Instance.Configuration.CheckRange;
                if (ImGui.InputInt(Lang.CheckBlockRange, ref checkRange))
                {
                    Plugin.Instance.Configuration.CheckRange = Math.Max(1, checkRange);
                    if (ImGui.IsItemDeactivatedAfterEdit())
                    {
                        Plugin.Instance.Configuration.Save();
                    }
                }

                //检查间隔
                var checkMillisecond = Plugin.Instance.Configuration.CheckMillisecond;
                if (ImGui.InputInt(Lang.CheckIntervalMillisecond, ref checkMillisecond))
                {
                    Plugin.Instance.Configuration.CheckMillisecond = Math.Max(1, checkMillisecond);
                    if (ImGui.IsItemDeactivatedAfterEdit())
                    {
                        Plugin.Instance.Configuration.Save();
                    }
                }

                //默语提示
                bool isShowTipsInChat = Plugin.Instance.Configuration.IsShowTipsInChat;
                ImGui.Checkbox(Lang.IsShowEchoTips, ref isShowTipsInChat);
                if (Plugin.Instance.Configuration.IsShowTipsInChat != isShowTipsInChat)
                {
                    Plugin.Instance.Configuration.IsShowTipsInChat = isShowTipsInChat;
                    Plugin.Instance.Configuration.Save();
                }
                if (Plugin.Instance.Configuration.IsShowTipsInChat)
                {
                    var echoTips = Plugin.Instance.Configuration.EchoTips;
                    if (ImGui.InputText(Lang.EchoTipsTitle, ref echoTips, 20))
                    {
                        Plugin.Instance.Configuration.EchoTips = echoTips;
                        if (ImGui.IsItemDeactivatedAfterEdit())
                        {
                            Plugin.Instance.Configuration.Save();
                        }
                    }
                }

                //是否屏蔽好友
                bool isBlockFriend = Plugin.Instance.Configuration.IsBlockFriend;
                ImGui.Checkbox(Lang.IsBlockFriend, ref isBlockFriend);
                if (Plugin.Instance.Configuration.IsBlockFriend != isBlockFriend)
                {
                    Plugin.Instance.Configuration.IsBlockFriend = isBlockFriend;
                    Plugin.Instance.Configuration.Save();
                }

                //是否屏蔽队友
                bool isBlockParty = Plugin.Instance.Configuration.IsBlockParty;
                ImGui.Checkbox(Lang.IsBlockParty, ref isBlockParty);
                if (Plugin.Instance.Configuration.IsBlockParty != isBlockParty)
                {
                    Plugin.Instance.Configuration.IsBlockParty = isBlockParty;
                    Plugin.Instance.Configuration.Save();
                }

                //是否登录就显示本窗口
                bool isLoginedOpenWindow = Plugin.Instance.Configuration.IsLoginedOpenWindow;
                ImGui.Checkbox(Lang.LoginShow, ref isLoginedOpenWindow);
                if (Plugin.Instance.Configuration.IsLoginedOpenWindow != isLoginedOpenWindow)
                {
                    Plugin.Instance.Configuration.IsLoginedOpenWindow = isLoginedOpenWindow;
                    Plugin.Instance.Configuration.Save();
                }

                //禁用esc关闭，仅可使用x关闭
                bool isEscCloseWindow = Plugin.Instance.Configuration.IsEscCloseWindow;
                ImGui.Checkbox(Lang.EscClose, ref isEscCloseWindow);
                if (Plugin.Instance.Configuration.IsEscCloseWindow != isEscCloseWindow)
                {
                    Plugin.Instance.Configuration.IsEscCloseWindow = isEscCloseWindow;
                    RespectCloseHotkey = isEscCloseWindow;
                    Plugin.Instance.Configuration.Save();
                }

                //是否快捷右键添加屏蔽玩家
                bool isRightClickAddShortcut = Plugin.Instance.Configuration.IsRightClickAddShortcut;
                ImGui.Checkbox(Lang.RightAddBlock, ref isRightClickAddShortcut);
                if (Plugin.Instance.Configuration.IsRightClickAddShortcut != isRightClickAddShortcut)
                {
                    Plugin.Instance.Configuration.IsRightClickAddShortcut = isRightClickAddShortcut;
                    Plugin.Instance.Configuration.Save();
                }

                ImGui.EndTabItem();
            } 

            //关于
            if (ImGui.BeginTabItem(Lang.About))
            {
                //反馈问题
                if (ImGui.Button(Lang.SendIssue))
                {
                    var url = "https://discord.gg/GWMEY9P9BX";
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        public void Dispose()
        {

        }
    }
}
