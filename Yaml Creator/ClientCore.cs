using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.MessageLog.Messages;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Windows.Forms;
using YargArchipelagoCommon;
using YargArchipelagoPlugin;
using static YargArchipelagoCommon.CommonData;

namespace Yaml_Creator
{
    public class ClientSession : BaseConnectionContainer
    {
        public Timer timer;
        public readonly ConcurrentQueue<LogMessage> _chatQueue = new ConcurrentQueue<LogMessage>();
        public bool _hasItemUpdate = false;
        public bool _hasHintUpdate = false;
    }
    public partial class MainForm : Form
    {
        ClientSession ClientConnection;

        private void InitializeClientComponents()
        {
            ClientConnection = new ClientSession();
            CreateClientListeners();
            rtbClientItems.Resize += listView1_Resize;
            rtbClientItems.ItemSelectionChanged += (s, e) => { e.Item.Selected = false; };
            rtbClientItems.MouseDoubleClick += listView1_MouseDoubleClick;
            listView1_Resize(rtbClientItems, EventArgs.Empty);

            string ConnectionFile = CrossPlatformFileLoader.TryGetConnectionJson();
            if (ConnectionFile != null)
            {
                var ConnectionDetails = Utility.TryParseConnectionDetails(ConnectionFile, out string error);
                if (ConnectionDetails != null)
                {
                    txtClientAddress.Text = ConnectionDetails.Address;
                    txtClientPass.Text = ConnectionDetails.Password;
                    txtClientSlot.Text = ConnectionDetails.SlotName;
                }
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (!ClientConnection.ClientConnected) return;
            EmptyChatQueue();
            if (ClientConnection._hasItemUpdate || ClientConnection._hasHintUpdate)
                ClientConnection.UpdateReceivedItems((s) => Debug.WriteLine(s));
            if (ClientConnection._hasItemUpdate)
            {
                ClientConnection._hasItemUpdate = false;
                UpdateLocationList();
                UpdateItemsList();
                UpdateSeedStatus();
            }
            if (ClientConnection._hasHintUpdate)
            {
                ClientConnection._hasHintUpdate = false;
                UpdateHintList();
            }
        }

        private void UpdateSeedStatus()
        {
            lbSeedStatus.Items.Clear();
            lbSeedStatus.Items.Add($"Goal Song Unlocked: {ClientConnection.SlotData.GoalData.IsSongUnlocked(ClientConnection)}");
            lbSeedStatus.Items.Add($"===================================================");
            if (ClientConnection.SlotData.SetlistNeededForGoal > 0)
            {
                lbSeedStatus.Items.Add($"Song Completions Needed: {ClientConnection.SlotData.SetlistNeededForGoal}");
                lbSeedStatus.Items.Add($"Songs Completed: {ClientConnection.ApItemsRecieved.Count(x => x.Type == StaticItems.SongCompletion)}");
                lbSeedStatus.Items.Add($"===================================================");
            }
            if (ClientConnection.SlotData.FamePointsForGoal > 0)
            {
                lbSeedStatus.Items.Add($"Fame Points Needed: {ClientConnection.SlotData.FamePointsForGoal}");
                lbSeedStatus.Items.Add($"Fame Points Aquired: {ClientConnection.ApItemsRecieved.Count(x => x.Type == StaticItems.FamePoint)}");
                lbSeedStatus.Items.Add($"===================================================");
            }
            if (ClientConnection.GoalItemInPool(out var recieved, out var recieveInfo))
            {
                lbSeedStatus.Items.Add($"Goal Song Item Found: {recieved}");
                lbSeedStatus.Items.Add($"From: {recieveInfo.GetPlayerInfo(ClientConnection).Name} at {recieveInfo.GetLocationsName(ClientConnection)} playing {recieveInfo.SendingPlayerGame}");
                lbSeedStatus.Items.Add($"===================================================");
            }
        }

        private void UpdateLocationList()
        {
            List<(string Pool, string LocationName)> ToPrint = new List<(string Pool, string LocationName)>();
            foreach (var i in ClientConnection.SlotData.Songs)
            {
                if (!i.IsSongUnlocked(ClientConnection)) continue;
                bool PrintedAny = false;
                if (ClientConnection.GetSession().Locations.AllMissingLocations.Contains(i.MainLocationID))
                {
                    ToPrint.Add((i.PoolName, ClientConnection.GetSession().Locations.GetLocationNameFromId(i.MainLocationID)));
                    PrintedAny = true;
                }
                if (i.ExtraLocationID > 0 && ClientConnection.GetSession().Locations.AllMissingLocations.Contains(i.ExtraLocationID))
                {
                    ToPrint.Add((i.PoolName, ClientConnection.GetSession().Locations.GetLocationNameFromId(i.ExtraLocationID)));
                    PrintedAny = true;
                }
                if (!PrintedAny && i.ExtraLocationID > 0 && ClientConnection.GetSession().Locations.AllMissingLocations.Contains(i.CompletionLocationID))
                    ToPrint.Add((i.PoolName, ClientConnection.GetSession().Locations.GetLocationNameFromId(i.CompletionLocationID)));
            }
            var Result = ToPrint.OrderBy(x => x.Pool).ThenBy(x => x.LocationName).Select(x => x.LocationName).ToList();
            rtbClientLocations.Clear();
            rtbClientLocations.AppendMessages(Result.ToArray());
        }

        private void UpdateItemsList()
        {
            List<ColoredString> ToPrint = new List<ColoredString>();
            Dictionary<StaticItems, List<BaseYargAPItem>> Recived = new Dictionary<StaticItems, List<BaseYargAPItem>>();
            rtbClientItems.Items.Clear();
            foreach (var item in ClientConnection.ReceivedInstruments)
            {
                var Entry = new ListViewItem(new string[] { 1.ToString(), item.Key.GetDescription() }) { ForeColor = GetColor(ItemFlags.Advancement) };
                Entry.Tag = item.Value;
                rtbClientItems.Items.Add(Entry);
            }

            var AllSongs = ClientConnection.ReceivedSongUnlockItems.ToDictionary(x => ClientConnection.GetSession().Items.GetItemName(x.Key), x => x.Value).OrderBy(x => x.Key);
            foreach (var item in AllSongs)
            {
                var Entry = new ListViewItem(new string[] { 1.ToString(), item.Key }) { ForeColor = GetColor(ItemFlags.Advancement) };
                Entry.Tag = item.Value;
                rtbClientItems.Items.Add(Entry);
            }

            foreach (var i in ClientConnection.ApItemsRecieved)
            {
                if (!Recived.ContainsKey(i.Type)) Recived.Add(i.Type, new List<BaseYargAPItem>());
                Recived[i.Type].Add(i);
            }
            foreach(var item in Recived)
            {
                var Entry = new ListViewItem([item.Value.Count.ToString(), item.Key.GetDescription()]);
                Entry.ForeColor = GetColor(ClientConnection.ItemPriorities[item.Key]);
                Entry.Tag = item.Value.ToArray();
                rtbClientItems.Items.Add(Entry);
            }
        }
        private void listView1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            var hit = rtbClientItems.HitTest(e.Location);
            if (hit.Item == null) return;

            var item = hit.Item;
            if (item?.Tag is null)
                return;
            BaseYargAPItem[] RecieveInfo = Array.Empty<BaseYargAPItem>();
            if (item.Tag is BaseYargAPItem yargItem)
                RecieveInfo = new BaseYargAPItem[] { yargItem };
            else if (item.Tag is BaseYargAPItem[] yargItems)
                RecieveInfo = yargItems;
            string Header = item.SubItems[1].Text;
            StringBuilder sb = new StringBuilder($"Found by: ");
            foreach(var data in RecieveInfo)
            {
                sb.AppendLine();
                sb.AppendLine();
                var Player = ClientConnection.GetSession().Players.GetPlayerInfo(data.SendingPlayerSlot);
                var Location = ClientConnection.GetSession().Locations.GetLocationNameFromId(data.SendingPlayerLocation, Player.Game);
                sb.Append($"{Player.Name}");
                if (Player != 0)
                {
                    sb.Append($" Playing {data.SendingPlayerGame}");
                    if (!String.IsNullOrWhiteSpace(Location))
                        sb.Append($" at:\n{Location}");
                }
            }
            MessageBox.Show(sb.ToString(), Header);
        }
        private void listView1_Resize(object sender, EventArgs e)
        {
            if (rtbClientItems.View != View.Details || rtbClientItems.Columns.Count < 2) return;
            int col0 = rtbClientItems.Columns[0].Width;
            int padding = SystemInformation.VerticalScrollBarWidth + 4;
            int newWidth = rtbClientItems.ClientSize.Width - col0 - padding;
            if (newWidth < 50) newWidth = 50;
            rtbClientItems.Columns[1].Width = newWidth;
        }

        private void UpdateHintList()
        {
            var Hints = ClientConnection.GetSession().Hints.GetHints();
            List<ColoredString> Print = new List<ColoredString>();
            foreach(var hint in Hints)
            {
                ColoredString str = new ColoredString();
                var FindingPlayer = ClientConnection.GetSession().Players.GetPlayerInfo(hint.FindingPlayer);
                var RecievingPlayer = ClientConnection.GetSession().Players.GetPlayerInfo(hint.ReceivingPlayer);
                var Location = ClientConnection.GetSession().Locations.GetLocationNameFromId(hint.LocationId, FindingPlayer.Game);
                var Item = ClientConnection.GetSession().Items.GetItemName(hint.ItemId, RecievingPlayer.Game);
                str.AddPart(RecievingPlayer.Name, GetColor(RecievingPlayer, ClientConnection.GetSession().ConnectionInfo), true)
                    .AddPart("'s", WithSpace: true)
                    .AddPart(Item, GetColor(hint.ItemFlags), true)
                    .AddPart("is at ", WithSpace: true)
                    .AddPart(Location, Color.Green, true);
                if (!string.IsNullOrWhiteSpace(hint.Entrance))
                    str.AddPart(hint.Entrance, Color.Blue, true);
                str.AddPart("in ", WithSpace: true)
                    .AddPart(FindingPlayer.Name, GetColor(FindingPlayer, ClientConnection.GetSession().ConnectionInfo), true)
                    .AddPart("'s world ", WithSpace: true)
                    .AddPart($"({hint.Status})", GetColor(hint));
                Print.Add(str);
            }
            rtbClientHints.Clear();
            rtbClientHints.AppendMessages(Print.ToArray());
        }

        private void EmptyChatQueue()
        {
            if (ClientConnection._chatQueue.IsEmpty) return;

            var batch = new List<LogMessage>();

            while (ClientConnection._chatQueue.TryDequeue(out var msg))
                batch.Add(msg);

            if (batch.Count == 0) return;

            bool wasAtBottom = rtbClientChat.IsScrolledToBottom();
            rtbClientChat.BeginUpdate();
            rtbClientChat.AppendMessages(batch.ToArray());
            rtbClientChat.EndUpdate();
            if (wasAtBottom)
            {
                rtbClientChat.SelectionStart = rtbClientChat.TextLength;
                rtbClientChat.ScrollToCaret();
            }
        }
        List<string> MessageHistory = new List<string>();
        int _hist = -1;
        string _draft = "";
        private void CreateClientListeners()
        {
            ClientConnection.timer = new Timer();
            ClientConnection.timer.Interval = 200;
            ClientConnection.timer.Tick += Timer_Tick;
            ClientConnection.timer.Start();
            btnClientConnect.Click += ToggleClientConenction;
            btnClientSend.Click += BtnClientSend_Click;
            txtClientAddress.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; ToggleClientConenction(s, e); } };
            txtClientSlot.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; ToggleClientConenction(s, e); } };
            txtClientPass.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; ToggleClientConenction(s, e); } };
            txtClientMessageInput.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; BtnClientSend_Click(s, e); return; }
                if (e.KeyCode == Keys.Up) { e.SuppressKeyPress = true; HistoryUp(); return; }
                if (e.KeyCode == Keys.Down) { e.SuppressKeyPress = true; HistoryDown(); return; }
            };
        }
        void HistoryUp()
        {
            if (MessageHistory.Count == 0) return;
            if (_hist == -1) _draft = txtClientMessageInput.Text;
            _hist = Math.Min(_hist + 1, MessageHistory.Count - 1);
            txtClientMessageInput.Text = MessageHistory[MessageHistory.Count - 1 - _hist];
            txtClientMessageInput.SelectionStart = txtClientMessageInput.TextLength;
        }

        void HistoryDown()
        {
            if (_hist == -1) return;
            _hist--;
            txtClientMessageInput.Text = _hist == -1 ? _draft : MessageHistory[MessageHistory.Count - 1 - _hist];
            txtClientMessageInput.SelectionStart = txtClientMessageInput.TextLength;
        }

        private void BtnClientSend_Click(object sender, EventArgs e)
        {
            if (!ClientConnection.ClientConnected || string.IsNullOrWhiteSpace(txtClientMessageInput.Text)) return;
            ClientConnection.GetSession().Say(txtClientMessageInput.Text);
            MessageHistory.Add(txtClientMessageInput.Text);
            txtClientMessageInput.Text = string.Empty;
        }

        private bool ConnectClient()
        {
            if (string.IsNullOrWhiteSpace(txtClientAddress.Text) || string.IsNullOrWhiteSpace(txtClientSlot.Text)) return false;
            var (Ip, Port) = YargAPUtils.ParseIpAddress(txtClientAddress.Text);
            if (Ip is null || Port < 0) return false;
            rtbClientChat.AppendMessages($"Connecting to {txtClientSlot.Text}@{Ip}:{Port}");
            var TempSession = ArchipelagoSessionFactory.CreateSession(Ip, Port);
            var Result = TempSession.TryConnectAndLogin("YAYARG", txtClientSlot.Text, Archipelago.MultiClient.Net.Enums.ItemsHandlingFlags.AllItems, new Version(0, 6, 1), password: txtClientPass.Text);
            if (Result is LoginFailure F)
            {
                rtbClientChat.AppendMessages($"Failed to Connect to {txtClientSlot.Text}@{txtClientAddress.Text}", string.Join("\n", F.Errors));
                return false;
            }
            rtbClientChat.AppendMessages($"Connected to {txtClientSlot.Text}@{txtClientAddress.Text}");
            ClientConnection.Session = TempSession;
            ClientConnection.SlotData = YargSlotData.Parse(ClientConnection.GetSession().DataStorage.GetSlotData());
            CreateAPListeners();
            ClientConnection._hasItemUpdate = true;
            ClientConnection._hasHintUpdate = true;
            return true;
        }
        private void DisconnectClient()
        {
            if (ClientConnection.GetSession().Socket.Connected)
                ClientConnection.GetSession().Socket.DisconnectAsync();
            ClientConnection.Session = null;
            ClientConnection.SlotData = null;
            ClientConnection.ApItemsRecieved.Clear();
            ClientConnection.ReceivedInstruments.Clear();
            ClientConnection.ReceivedSongUnlockItems.Clear();
            rtbClientLocations.Clear();
            rtbClientItems.Items.Clear();
            rtbClientHints.Clear();
            rtbClientChat.AppendMessages($"Disconnected From Archipelago");
        }
        private void ToggleClientConenction(object sender, EventArgs e)
        {
            if (ClientConnection.ClientConnected)
                DisconnectClient();
            else
                ConnectClient();
            btnClientConnect.Text = ClientConnection.ClientConnected ? "Disconnect" : "Connect";
        }

        private void CreateAPListeners()
        {
            ClientConnection.GetSession().MessageLog.OnMessageReceived += MessageLog_OnMessageReceived;
            ClientConnection.GetSession().Items.ItemReceived += Items_ItemReceived;
            ClientConnection.GetSession().Locations.CheckedLocationsUpdated += Locations_CheckedLocationsUpdated; ;
        }
        private void RemoveAPListeners()
        {
            ClientConnection.GetSession().MessageLog.OnMessageReceived -= MessageLog_OnMessageReceived;
            ClientConnection.GetSession().Items.ItemReceived -= Items_ItemReceived;
            ClientConnection.GetSession().Locations.CheckedLocationsUpdated -= Locations_CheckedLocationsUpdated; ;
        }

        private void Locations_CheckedLocationsUpdated(System.Collections.ObjectModel.ReadOnlyCollection<long> newCheckedLocations)
        {
            ClientConnection._hasItemUpdate = true;
        }

        private void Items_ItemReceived(Archipelago.MultiClient.Net.Helpers.ReceivedItemsHelper helper)
        {
            ClientConnection._hasItemUpdate = true;
        }

        private void MessageLog_OnMessageReceived(Archipelago.MultiClient.Net.MessageLog.Messages.LogMessage message)
        {
            ClientConnection._chatQueue.Enqueue(message);
            if (message is HintItemSendLogMessage)
                ClientConnection._hasHintUpdate = true;
        }

        private Color GetColor(ItemFlags flag)
        {
            var AdvancementItem = Archipelago.MultiClient.Net.Colors.ColorUtils.GetColor(flag).Value;
            var APColor = Archipelago.MultiClient.Net.Colors.BuiltInPalettes.Dark[AdvancementItem];
            return Color.FromArgb(APColor.R, APColor.G, APColor.B);
        }

        private Color GetColor(Archipelago.MultiClient.Net.Models.Hint hint)
        {
            var PalletColor = Archipelago.MultiClient.Net.Colors.ColorUtils.GetColor(hint);
            if (PalletColor is null) return Color.White;
            var AdvancementItem = PalletColor.Value;
            var APColor = Archipelago.MultiClient.Net.Colors.BuiltInPalettes.Dark[AdvancementItem];
            return Color.FromArgb(APColor.R, APColor.G, APColor.B);
        }

        private Color GetColor(Archipelago.MultiClient.Net.Models.ItemInfo item)
        {
            var AdvancementItem = Archipelago.MultiClient.Net.Colors.ColorUtils.GetColor(item).Value;
            var APColor = Archipelago.MultiClient.Net.Colors.BuiltInPalettes.Dark[AdvancementItem];
            return Color.FromArgb(APColor.R, APColor.G, APColor.B);
        }

        private Color GetColor(PlayerInfo player, IConnectionInfoProvider connectionInfo)
        {
            var AdvancementItem = Archipelago.MultiClient.Net.Colors.ColorUtils.GetColor(player, connectionInfo).Value;
            var APColor = Archipelago.MultiClient.Net.Colors.BuiltInPalettes.Dark[AdvancementItem];
            return Color.FromArgb(APColor.R, APColor.G, APColor.B);
        }

    }
}
