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
using ImGuiNET;
using ImGuiScene;
using Microsoft.VisualBasic.Logging;
using Dalamud.Interface.Utility.Table;
using System.Data.Common;
using System.Diagnostics;

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
            ImGui.BeginTabBar(Plugin.Instance.Name);

            if (ImGui.BeginTabItem("选择你想屏蔽的种族"))
            {
                ImGui.BeginTable("raceTable", 3, ImGuiTableFlags.NoBordersInBody | ImGuiTableFlags.SizingFixedSame);

                ImGui.TableSetupColumn("种族");
                ImGui.TableSetupColumn("男");
                ImGui.TableSetupColumn("女");
                ImGui.TableHeadersRow();

                foreach (var item in Plugin.Instance.Configuration.ByteToRace)
                {
                    if (item.Key == 0)
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
                        Plugin.Instance.Configuration.Save();
                    }
                    ImGui.PopID();

                    ImGui.TableSetColumnIndex(2);
                    ImGui.PushID(item.Key + 200);
                    bool isHideFemale = item.Value.IsHideFemale;
                    ImGui.Checkbox("", ref isHideFemale);
                    if (item.Value.IsHideFemale != isHideFemale)
                    {
                        item.Value.IsHideFemale = isHideFemale;
                        Plugin.Instance.Configuration.Save();
                    }

                    ImGui.PopID();

                }
                ImGui.EndTable();

                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("指定的屏蔽名单"))
            {
                ImGui.BeginTable("targetRoleTable", 3, ImGuiTableFlags.NoBordersInBody | ImGuiTableFlags.SizingFixedSame);

                ImGui.TableSetupColumn("角色名");
                ImGui.TableSetupColumn("服务器");
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
                    if (ImGui.Button("移除"))
                    {
                        removeKey = item.Key;
                    }
                    ImGui.PopID();
                }
                if (removeKey != 0)
                {
                    Plugin.Instance.Configuration.BlockTargetRoleDic.Remove(removeKey);
                    Plugin.Instance.Configuration.Save();
                }
                ImGui.EndTable();

                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("设置"))
            {
                var checkRange = Plugin.Instance.Configuration.CheckRange;
                if (ImGui.InputInt("附近屏蔽人数检测范围(m)", ref checkRange))
                {
                    Plugin.Instance.Configuration.CheckRange = Math.Max(1, checkRange);
                    if (ImGui.IsItemDeactivatedAfterEdit())
                    {
                        Plugin.Instance.Configuration.Save();
                    }
                }

                bool isLoginedOpenWindow = Plugin.Instance.Configuration.IsLoginedOpenWindow;
                ImGui.Checkbox("登录就显示窗口", ref isLoginedOpenWindow);
                if (Plugin.Instance.Configuration.IsLoginedOpenWindow != isLoginedOpenWindow)
                {
                    Plugin.Instance.Configuration.IsLoginedOpenWindow = isLoginedOpenWindow;
                    Plugin.Instance.Configuration.Save();
                }

                //禁用esc关闭，仅可使用x关闭
                bool isEscCloseWindow = Plugin.Instance.Configuration.IsEscCloseWindow;
                ImGui.Checkbox("Esc关闭窗口", ref isEscCloseWindow);
                if (Plugin.Instance.Configuration.IsEscCloseWindow != isEscCloseWindow)
                {
                    Plugin.Instance.Configuration.IsEscCloseWindow = isEscCloseWindow;
                    RespectCloseHotkey = isEscCloseWindow;
                    Plugin.Instance.Configuration.Save();
                }

                bool isRightClickAddShortcut = Plugin.Instance.Configuration.IsRightClickAddShortcut;
                ImGui.Checkbox("右键添加快捷屏蔽", ref isRightClickAddShortcut);
                if (Plugin.Instance.Configuration.IsRightClickAddShortcut != isRightClickAddShortcut)
                {
                    Plugin.Instance.Configuration.IsRightClickAddShortcut = isRightClickAddShortcut;
                    Plugin.Instance.Configuration.Save();
                }

                //todo:每次遇到的时候刷新一下玩家名？

                ImGui.EndTabItem();
            } 

            if (ImGui.BeginTabItem("关于"))
            {
                if (ImGui.Button("反馈问题"))
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
