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
    public partial class MainForm : Form
    {
        private ArchipelagoSession session;
        private YargSlotData SlotData;
        private bool ClientConnected => session?.Socket != null && session.Socket.Connected;
        private Timer timer;
        private readonly ConcurrentQueue<LogMessage> _chatQueue = new ConcurrentQueue<LogMessage>();
        private bool _hasItemUpdate = false;
        private bool _hasHintUpdate = false;
        public Dictionary<long, BaseYargAPItem> ReceivedSongUnlockItems { get; } = new Dictionary<long, BaseYargAPItem>();
        public Dictionary<SupportedInstrument, BaseYargAPItem> ReceivedInstruments { get; } = new Dictionary<SupportedInstrument, BaseYargAPItem>();
        public HashSet<StaticYargAPItem> ApItemsRecieved { get; } = new HashSet<StaticYargAPItem>();

        private void InitializeClientComponents()
        {
            CreateClientListeners();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (!ClientConnected) return;
            EmptyChatQueue();
            UpdateReceivedItems();
            if (_hasItemUpdate)
            {
                _hasItemUpdate = false;
                UpdateLocationList();
                UpdateItemsList();
            }
            if (_hasHintUpdate)
            {
                _hasHintUpdate = false;
                UpdateHintList();
            }
        }

        private void UpdateLocationList()
        {
            List<(string Pool, string LocationName)> ToPrint = new List<(string Pool, string LocationName)>();
            foreach (var i in SlotData.Songs)
            {
                if (!IsSongUnlocked(i)) continue;
                bool PrintedAny = false;
                if (session.Locations.AllMissingLocations.Contains(i.MainLocationID))
                {
                    ToPrint.Add((i.PoolName, session.Locations.GetLocationNameFromId(i.MainLocationID)));
                    PrintedAny = true;
                }
                if (i.ExtraLocationID > 0 && session.Locations.AllMissingLocations.Contains(i.ExtraLocationID))
                {
                    ToPrint.Add((i.PoolName, session.Locations.GetLocationNameFromId(i.ExtraLocationID)));
                    PrintedAny = true;
                }
                if (!PrintedAny && i.ExtraLocationID > 0 && session.Locations.AllMissingLocations.Contains(i.CompletionLocationID))
                    ToPrint.Add((i.PoolName, session.Locations.GetLocationNameFromId(i.CompletionLocationID)));
            }
            var Result = ToPrint.OrderBy(x => x.Pool).ThenBy(x => x.LocationName).Select(x => x.LocationName).ToList();
            rtbClientLocations.Clear();
            rtbClientLocations.AppendMessages(Result.ToArray());
        }

        public static Dictionary<StaticItems, Archipelago.MultiClient.Net.Enums.ItemFlags> ItemPriorities = new Dictionary<StaticItems, Archipelago.MultiClient.Net.Enums.ItemFlags>();

        private void UpdateItemsList()
        {
            List<ColoredString> ToPrint = new List<ColoredString>();
            Dictionary<StaticItems, int> Recived = new Dictionary<StaticItems, int>();
            rtbClientItems.Clear();
            var InstrumentMessages = ReceivedInstruments.Keys.Select(i => new ColoredString(i.GetDescription(), GetColor(Archipelago.MultiClient.Net.Enums.ItemFlags.Advancement)));
            rtbClientItems.AppendMessages(InstrumentMessages.ToArray());

            var AllSongs = ReceivedSongUnlockItems.Select(x => session.Items.GetItemName(x.Key)).OrderBy(x => x)
                .Select(i => new ColoredString(i, GetColor(Archipelago.MultiClient.Net.Enums.ItemFlags.Advancement)));
            rtbClientItems.AppendMessages(AllSongs.ToArray());

            foreach (var i in ApItemsRecieved)
            {
                if (!Recived.ContainsKey(i.Type)) Recived.Add(i.Type, 0);
                Recived[i.Type]++;
            }
            rtbClientItems.AppendMessages(Recived.Select(i => new ColoredString($"{i.Value}X {i.Key.GetDescription()}", GetColor(ItemPriorities[i.Key]))).ToArray());
        }

        private void UpdateHintList()
        {
            var Hints = session.Hints.GetHints();
            List<ColoredString> Print = new List<ColoredString>();
            foreach(var hint in Hints)
            {
                ColoredString str = new ColoredString();
                var FindingPlayer = session.Players.GetPlayerInfo(hint.FindingPlayer);
                var RecievingPlayer = session.Players.GetPlayerInfo(hint.ReceivingPlayer);
                var Location = session.Locations.GetLocationNameFromId(hint.LocationId, FindingPlayer.Game);
                var Item = session.Items.GetItemName(hint.ItemId, RecievingPlayer.Game);
                str.AddPart(RecievingPlayer.Name, GetColor(RecievingPlayer, session.ConnectionInfo), true)
                    .AddPart("'s", WithSpace: true)
                    .AddPart(Item, GetColor(hint.ItemFlags), true)
                    .AddPart("is at ", WithSpace: true)
                    .AddPart(Location, Color.Green, true);
                if (!string.IsNullOrWhiteSpace(hint.Entrance))
                    str.AddPart(hint.Entrance, Color.Blue, true);
                str.AddPart("in ", WithSpace: true)
                    .AddPart(FindingPlayer.Name, GetColor(FindingPlayer, session.ConnectionInfo), true)
                    .AddPart("'s world ", WithSpace: true)
                    .AddPart($"({hint.Status})", GetColor(hint));
                Print.Add(str);
            }
            rtbClientHints.Clear();
            rtbClientHints.AppendMessages(Print.ToArray());
        }

        private bool IsSongUnlocked(SongAPData song)
        {
            var HasUnlockItem = ReceivedSongUnlockItems.ContainsKey(song.UnlockItemID);
            var HasInstrument = ReceivedInstruments.ContainsKey(song.GetPool(SlotData).instrument);
            return HasUnlockItem && HasInstrument;
        }

        public void UpdateReceivedItems()
        {
            Dictionary<StaticItems, int> ServerLocProxy = new Dictionary<StaticItems, int>();
            foreach (var i in session.Items.AllItemsReceived)
            {
                if (StaticItemsById.TryGetValue(i.ItemId, out var item))
                {
                    if (i.Player.Slot == 0)
                    {
                        if (!ServerLocProxy.ContainsKey(item)) ServerLocProxy[item] = 0;
                        ServerLocProxy[item]++;
                    }
                    ApItemsRecieved.Add(new StaticYargAPItem(item, i.ItemId, i.Player.Slot, i.Player.Slot == 0 ? ServerLocProxy[item] : i.LocationId, i.LocationGame));
                    if (!ItemPriorities.ContainsKey(item)) 
                        ItemPriorities[item] = i.Flags;
                }
                else if (InstrumentItemsById.TryGetValue(i.ItemId, out var instrument))
                    ReceivedInstruments[instrument] = new BaseYargAPItem(i.ItemId, i.Player.Slot, i.LocationId, i.LocationGame);
                else if (SlotData.SongUnlockIds.Contains(i.ItemId))
                    ReceivedSongUnlockItems[i.ItemId] = new BaseYargAPItem(i.ItemId, i.Player.Slot, i.LocationId, i.LocationGame);
                else
                    Debug.WriteLine($"Received unknown item {i.ItemName} [{i.ItemId}]");
            }
        }

        private void EmptyChatQueue()
        {
            if (_chatQueue.IsEmpty) return;

            var batch = new List<LogMessage>();

            while (_chatQueue.TryDequeue(out var msg))
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

        private void CreateClientListeners()
        {
            timer = new Timer();
            timer.Interval = 200;
            timer.Tick += Timer_Tick;
            timer.Start();
            btnClientConnect.Click += ToggleClientConenction;
            btnClientSend.Click += BtnClientSend_Click;
        }

        private void BtnClientSend_Click(object sender, EventArgs e)
        {
            if (!ClientConnected || string.IsNullOrWhiteSpace(txtClientMessageInput.Text)) return;
            session.Say(txtClientMessageInput.Text);
            txtClientMessageInput.Text = string.Empty;
        }

        private bool ConnectClient()
        {
            var (Ip, Port) = YargAPUtils.ParseIpAddress(txtClientAddress.Text);
            rtbClientChat.AppendMessages($"Connecting to {txtClientSlot.Text}@{Ip}:{Port}");
            var TempSession = ArchipelagoSessionFactory.CreateSession(Ip, Port);
            var Result = TempSession.TryConnectAndLogin("YAYARG", txtClientSlot.Text, Archipelago.MultiClient.Net.Enums.ItemsHandlingFlags.AllItems, new Version(0, 6, 1), password: txtClientPass.Text);
            if (Result is LoginFailure F)
            {
                rtbClientChat.AppendMessages($"Failed to Connect to {txtClientSlot.Text}@{txtClientAddress.Text}", string.Join("\n", F.Errors));
                return false;
            }
            rtbClientChat.AppendMessages($"Connected to {txtClientSlot.Text}@{txtClientAddress.Text}");
            session = TempSession;
            SlotData = YargSlotData.Parse(session.DataStorage.GetSlotData());
            CreateAPListeners();
            _hasItemUpdate = true;
            _hasHintUpdate = true;
            return true;
        }
        private void DisConnectClient()
        {
            if (session.Socket.Connected)
                session.Socket.DisconnectAsync();
            session = null;
            SlotData = null;
            rtbClientLocations.Clear();
            rtbClientItems.Clear();
            rtbClientHints.Clear();
            rtbClientChat.AppendMessages($"Disconnected From Archipelago");
        }
        private void ToggleClientConenction(object sender, EventArgs e)
        {
            if (ClientConnected)
            {
                DisConnectClient();
            }
            else
            {
                ConnectClient();
            }
            btnClientConnect.Text = ClientConnected ? "Disconnect" : "Connect";
        }

        private void CreateAPListeners()
        {
            session.MessageLog.OnMessageReceived += MessageLog_OnMessageReceived;
            session.Items.ItemReceived += Items_ItemReceived;
            session.Locations.CheckedLocationsUpdated += Locations_CheckedLocationsUpdated; ;
        }
        private void RemoveAPListeners()
        {
            session.MessageLog.OnMessageReceived -= MessageLog_OnMessageReceived;
            session.Items.ItemReceived -= Items_ItemReceived;
            session.Locations.CheckedLocationsUpdated -= Locations_CheckedLocationsUpdated; ;
        }

        private void Locations_CheckedLocationsUpdated(System.Collections.ObjectModel.ReadOnlyCollection<long> newCheckedLocations)
        {
            _hasItemUpdate = true;
        }

        private void Items_ItemReceived(Archipelago.MultiClient.Net.Helpers.ReceivedItemsHelper helper)
        {
            _hasItemUpdate = true;
        }

        private void MessageLog_OnMessageReceived(Archipelago.MultiClient.Net.MessageLog.Messages.LogMessage message)
        {
            _chatQueue.Enqueue(message);
            if (message is HintItemSendLogMessage)
                _hasHintUpdate = true;
        }

        private Color GetColor(ItemFlags flag)
        {
            var AdvancementItem = Archipelago.MultiClient.Net.Colors.ColorUtils.GetColor(flag).Value;
            var APColor = Archipelago.MultiClient.Net.Colors.BuiltInPalettes.Dark[AdvancementItem];
            return Color.FromArgb(APColor.R, APColor.G, APColor.B);
        }

        private Color GetColor(Archipelago.MultiClient.Net.Models.Hint hint)
        {
            var AdvancementItem = Archipelago.MultiClient.Net.Colors.ColorUtils.GetColor(hint).Value;
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
