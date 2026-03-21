using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using ECommons.DalamudServices;
using System;
using System.Diagnostics;
using System.Numerics;

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
                var checkRange = config.CheckRange;
                if (ImGui.InputInt(Lang.CheckBlockRange, ref checkRange))
                {
                    config.CheckRange = Math.Max(1, checkRange);
                    if (ImGui.IsItemDeactivatedAfterEdit())
                    {
                        config.Save();
                    }
                }

                //检查间隔
                var checkMillisecond = config.CheckMillisecond;
                if (ImGui.InputInt(Lang.CheckIntervalMillisecond, ref checkMillisecond))
                {
                    config.CheckMillisecond = Math.Max(1, checkMillisecond);
                    if (ImGui.IsItemDeactivatedAfterEdit())
                    {
                        config.Save();
                    }
                }

                //默语提示
                bool isShowTipsInChat = config.IsShowTipsInChat;
                ImGui.Checkbox(Lang.IsShowEchoTips, ref isShowTipsInChat);
                if (config.IsShowTipsInChat != isShowTipsInChat)
                {
                    config.IsShowTipsInChat = isShowTipsInChat;
                    config.Save();
                }
                if (config.IsShowTipsInChat)
                {
                    var echoTips = config.EchoTips;
                    if (ImGui.InputText(Lang.EchoTipsTitle, ref echoTips, 20))
                    {
                        config.EchoTips = echoTips;
                        if (ImGui.IsItemDeactivatedAfterEdit())
                        {
                            config.Save();
                        }
                    }
                }

                //是否屏蔽好友
                bool isBlockFriend = config.IsBlockFriend;
                ImGui.Checkbox(Lang.IsBlockFriend, ref isBlockFriend);
                if (config.IsBlockFriend != isBlockFriend)
                {
                    config.IsBlockFriend = isBlockFriend;
                    config.Save();
                }

                //是否屏蔽队友
                bool isBlockParty = config.IsBlockParty;
                ImGui.Checkbox(Lang.IsBlockParty, ref isBlockParty);
                if (config.IsBlockParty != isBlockParty)
                {
                    config.IsBlockParty = isBlockParty;
                    config.Save();
                }

                //是否登录就显示本窗口
                bool isLoginedOpenWindow = config.IsLoginedOpenWindow;
                ImGui.Checkbox(Lang.LoginShow, ref isLoginedOpenWindow);
                if (config.IsLoginedOpenWindow != isLoginedOpenWindow)
                {
                    config.IsLoginedOpenWindow = isLoginedOpenWindow;
                    config.Save();
                }

                //禁用esc关闭，仅可使用x关闭
                bool isEscCloseWindow = config.IsEscCloseWindow;
                ImGui.Checkbox(Lang.EscClose, ref isEscCloseWindow);
                if (config.IsEscCloseWindow != isEscCloseWindow)
                {
                    config.IsEscCloseWindow = isEscCloseWindow;
                    RespectCloseHotkey = isEscCloseWindow;
                    config.Save();
                }

                //是否快捷右键添加屏蔽玩家
                bool isRightClickAddShortcut = config.IsRightClickAddShortcut;
                ImGui.Checkbox(Lang.RightAddBlock, ref isRightClickAddShortcut);
                if (config.IsRightClickAddShortcut != isRightClickAddShortcut)
                {
                    config.IsRightClickAddShortcut = isRightClickAddShortcut;
                    config.Save();
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
