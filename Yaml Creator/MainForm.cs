using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using YargArchipelagoPlugin;
using static Yaml_Creator.SongData;
using static Yaml_Creator.Utility;
using static YargArchipelagoCommon.CommonData;

namespace Yaml_Creator
{
    public partial class MainForm : Form
    {
        public static SongExportExtendedData[] ExportFile;
        public static YAMLCore YAML;
        public static string OutputFolder = Path.Combine(Application.StartupPath, "Output");
        public static SongPoolContainer SelectedSongPool = null;
        public bool IsLoadingNewSongPool = false;
        private const string cache = "cache";
        private ContextMenuStrip ctxMenu = new ContextMenuStrip();
        public MainForm()
        {
            InitializeComponent();

            if (File.Exists(cache))
            {
                try { YAML = JsonConvert.DeserializeObject<YAMLCore>(File.ReadAllText(cache)); }
                catch { YAML = null; }
            }
            if (YAML is null)
                YAML = new YAMLCore();

            LoadYamlToControls();
            CreateListeners();
            Directory.CreateDirectory(OutputFolder);
            txtPoolExclude.ReadOnly = true;
            txtPoolInclude.ReadOnly = true;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            var datas = CrossPlatformFileLoader.LoadSongDataCrossPlatform();
            if (datas == null)
            {
                this.Close();
                return;
            }
            ExportFile = datas.Select(x => new SongExportExtendedData(x)).ToArray();
            YAML.YAYARG.songList = SongDataConverter.ConvertSongDataToBase64(ExportFile);
            PrintActiveSongs(sender, e);
            SongPoolListUpdated();
            RegenerateGoalPoolList();
            RegenerateGoalSongList();
            ValidateIncludeExcludeList();
            LoadSongPool();
            cmbGoalPoolPlando.Enabled = chkGoalPoolPlando.Checked;
            cmbGoalSongPlando.Enabled = chkGoalSongPlando.Checked;
        }

        private void LoadSongPool()
        {
            IsLoadingNewSongPool = true;
            UpdatePoolCount();
            if (SelectedSongPool == null)
            {
                gbSelectedPool.Enabled = false;
                foreach(Control i in gbSelectedPool.Controls)
                    i.Enabled = false;
                nudAmountInPool.Value = 0;
                nudPoolMinDifficulty .Value = 0;
                nudPoolMaxDifficulty .Value = 0;
                cmbReward1Diff.SelectedItem = null;
                cmbReward2Diff.SelectedItem = null;
                cmbReward1Score.SelectedItem = null;
                cmbReward2Score.SelectedItem = null;
                IsLoadingNewSongPool = false;
                return;
            }
            gbSelectedPool.Enabled = true;
            foreach (Control i in gbSelectedPool.Controls)
                i.Enabled = true;
            nudAmountInPool.Value = SelectedSongPool.Pool.amount_in_pool;
            nudPoolMinDifficulty.Value = SelectedSongPool.Pool.min_difficulty;
            nudPoolMaxDifficulty.Value = SelectedSongPool.Pool.max_difficulty;
            cmbReward1Diff.SelectedItem = cmbReward1Diff.Items.Cast<DisplayItem<SupportedDifficulty>>().FirstOrDefault(x => x.Value == SelectedSongPool.Pool.completion_requirements.reward1_diff);
            cmbReward2Diff.SelectedItem = cmbReward2Diff.Items.Cast<DisplayItem<SupportedDifficulty>>().FirstOrDefault(x => x.Value == SelectedSongPool.Pool.completion_requirements.reward2_diff);
            cmbReward1Score.SelectedItem = cmbReward1Score.Items.Cast<DisplayItem<CompletionReq>>().FirstOrDefault(x => x.Value == SelectedSongPool.Pool.completion_requirements.reward1_req);
            cmbReward2Score.SelectedItem = cmbReward2Score.Items.Cast<DisplayItem<CompletionReq>>().FirstOrDefault(x => x.Value == SelectedSongPool.Pool.completion_requirements.reward2_req);
            IsLoadingNewSongPool = false;
            return;
        }

        private void UpdatePoolCount()
        {
            if (SelectedSongPool == null)
            {
                gbSelectedPool.Text = "N/A";
                return;
            }
            var MaxSongs = ExportFile.Count(x => x.core.ValidForPool(SelectedSongPool.Pool));
            gbSelectedPool.Text = $"{SelectedSongPool.Name}: {MaxSongs} Valid Songs";
        }

        private void SavePoolValues()
        {
            if (IsLoadingNewSongPool || SelectedSongPool == null)
                return;

            SelectedSongPool.Pool.amount_in_pool = (int)nudAmountInPool.Value;
            SelectedSongPool.Pool.min_difficulty = (int)nudPoolMinDifficulty.Value;
            SelectedSongPool.Pool.max_difficulty = (int)nudPoolMaxDifficulty.Value;
            SelectedSongPool.Pool.completion_requirements.reward1_diff = cmbReward1Diff.SelectedItem is DisplayItem<SupportedDifficulty> item1 ? item1.Value : SupportedDifficulty.Expert;
            SelectedSongPool.Pool.completion_requirements.reward2_diff = cmbReward2Diff.SelectedItem is DisplayItem<SupportedDifficulty> item2 ? item2.Value : SupportedDifficulty.Expert;
            SelectedSongPool.Pool.completion_requirements.reward1_req = cmbReward1Score.SelectedItem is DisplayItem<CompletionReq> item3 ? item3.Value : CompletionReq.Clear;
            SelectedSongPool.Pool.completion_requirements.reward2_req = cmbReward2Score.SelectedItem is DisplayItem<CompletionReq> item4 ? item4.Value : CompletionReq.Clear;

            UpdatePoolCount();
        }

        bool PrintingSongs = false;
        public void PrintActiveSongs(object sender, EventArgs e)
        {
            PrintingSongs = true;
            var ActiveSongs = FormHelpers.FilterItems(ExportFile, txtActiveSongFilter.Text, x => AddTags(x));
            ActiveSongs = ActiveSongs.OrderBy(x => x.ToString()).ToArray();
            lbActiveSongs.DataSource = ActiveSongs.Select(x => new TaggedSongExportExtendedData(x, AddTags)).ToArray();

            Debug.WriteLine(JsonConvert.SerializeObject(YAML.YAYARG.song_exclusion_list, Formatting.Indented));
            for (int i = 0; i < ActiveSongs.Length; i++)
                lbActiveSongs.SetItemChecked(i, !IsExcluded(ActiveSongs[i]));

            string AddTags(SongExportExtendedData extendedData)
            {
                StringBuilder stringBuilder = new StringBuilder();
                if (CurrentTypes.Contains(DisplayTypes.Source) && !string.IsNullOrWhiteSpace(extendedData.core.Source))
                    stringBuilder.Append($"[{extendedData.core.Source}] ");
                if (CurrentTypes.Contains(DisplayTypes.Genre) && !string.IsNullOrWhiteSpace(extendedData.core.Genre))
                    stringBuilder.Append($"[{extendedData.core.Genre}] ");
                if (CurrentTypes.Contains(DisplayTypes.Charter) && !string.IsNullOrWhiteSpace(extendedData.core.Charter))
                    stringBuilder.Append($"[{extendedData.core.Charter}] ");
                if (CurrentTypes.Contains(DisplayTypes.Name))
                    stringBuilder.Append($"{extendedData.core.Name} ");
                if (CurrentTypes.Contains(DisplayTypes.Artist))
                    stringBuilder.Append($"by {extendedData.core.Artist} ");
                if (CurrentTypes.Contains(DisplayTypes.Album))
                    stringBuilder.Append($"from {extendedData.core.Album} ");
                if (CurrentTypes.Contains(DisplayTypes.Hash))
                    stringBuilder.Append($"[{extendedData.core.SongChecksum}]");
                var final = stringBuilder.ToString();
                return string.IsNullOrWhiteSpace(final) ? extendedData.core.SongChecksum : stringBuilder.ToString();
            }
            bool IsExcluded(SongExportExtendedData song) => YAML.YAYARG.song_exclusion_list.Contains(song.core.SongChecksum);
            PrintingSongs = false;
        }

        private void CreateListeners()
        {
            txtActiveSongFilter.TextChanged += PrintActiveSongs;
            txtSlotName.TextChanged += (s, e) => YAML.name = txtSlotName.Text;
            //Song Check Settings
            nudSongExtra.ValueChanged += (s, e) => YAML.YAYARG.song_check_extra = (int)nudSongExtra.Value;
            nudSongPack.ValueChanged += (s, e) => YAML.YAYARG.song_pack_size = (int)nudSongPack.Value;
            nudStartingSongs.ValueChanged += (s, e) => YAML.YAYARG.starting_songs = (int)nudStartingSongs.Value;
            chkReuseSongs.CheckedChanged += (s, e) => YAML.YAYARG.reuse_songs = chkReuseSongs.Checked;
            chkInstrumentShuffle.CheckedChanged += (s, e) => YAML.YAYARG.instrument_shuffle = chkInstrumentShuffle.Checked;
            //Goal Song Settings
            nudGoalFame.ValueChanged += (s, e) => YAML.YAYARG.fame_point_needed = (int)nudGoalFame.Value;
            nudGoalSetlist.ValueChanged += (s, e) => YAML.YAYARG.setlist_needed = (int)nudGoalSetlist.Value;
            nudFameAmount.ValueChanged += (s, e) => YAML.YAYARG.fame_point_amount = (int)nudFameAmount.Value;
            chkGoalItemNeeded.CheckedChanged += (s, e) => YAML.YAYARG.goal_song_item_needed = chkGoalItemNeeded.Checked;
            //Link Options 
            cmbEnergyLink.DataSource = Utility.GetEnumDataSource<EnergyLinkType>();
            cmbDeathLink.DataSource = Utility.GetEnumDataSource<DeathLinkType>();
            cmbEnergyLink.SelectedIndexChanged += (s, e) => YAML.YAYARG.energy_link = (cmbEnergyLink.SelectedItem as DisplayItem<EnergyLinkType>)?.Value ?? EnergyLinkType.disabled;
            cmbDeathLink.SelectedIndexChanged += (s, e) => YAML.YAYARG.death_link = (cmbDeathLink.SelectedItem as DisplayItem<DeathLinkType>)?.Value ?? DeathLinkType.disabled;
            //Goal Plando
            chkGoalPoolPlando.CheckedChanged += (s, e) =>
            {
                cmbGoalPoolPlando.Enabled = chkGoalPoolPlando.Checked;
                cmbGoalPoolPlando.SelectedItem = null;
                YAML.YAYARG.goal_pool_plando = string.Empty;
                RegenerateGoalSongList();
            };
            chkGoalSongPlando.CheckedChanged += (s, e) =>
            {
                cmbGoalSongPlando.Enabled = chkGoalSongPlando.Checked;
                cmbGoalSongPlando.SelectedItem = null;
                YAML.YAYARG.goal_song_plando = string.Empty;
            };
            cmbGoalPoolPlando.SelectedIndexChanged += (s, e) =>
            {
                YAML.YAYARG.goal_pool_plando = chkGoalPoolPlando.Checked ? cmbGoalPoolPlando.SelectedItem?.ToString() ?? string.Empty : string.Empty;
                RegenerateGoalSongList();
            };
            cmbGoalSongPlando.SelectedIndexChanged += (s, e) =>
                YAML.YAYARG.goal_song_plando = chkGoalSongPlando.Checked && cmbGoalSongPlando.SelectedItem is DisplayItem<SongExportExtendedData> item
                    ? item.Value.core.SongChecksum
                    : string.Empty;
            //Filler Items
            nudStarPower.ValueChanged += (s, e) => YAML.YAYARG.star_power = (int)nudStarPower.Value;
            nudSwapPick.ValueChanged += (s, e) => YAML.YAYARG.swap_song_choice = (int)nudSwapPick.Value;
            nudSwapRandom.ValueChanged += (s, e) => YAML.YAYARG.swap_song_random = (int)nudSwapRandom.Value;
            nudLowerDiff.ValueChanged += (s, e) => YAML.YAYARG.lower_difficulty = (int)nudLowerDiff.Value;
            nudRestartTrap.ValueChanged += (s, e) => YAML.YAYARG.restart_trap = (int)nudRestartTrap.Value;
            nudRockTrap.ValueChanged += (s, e) => YAML.YAYARG.rock_meter_trap = (int)nudRockTrap.Value;
            nudNothingItem.ValueChanged += (s, e) => YAML.YAYARG.nothing = (int)nudNothingItem.Value;
            nudFailPrevention.ValueChanged += (s, e) => YAML.YAYARG.fail_prevention = (int)nudFailPrevention.Value;
            //Song Pools
            cmbReward1Diff.DataSource = Utility.GetEnumDataSource<SupportedDifficulty>();
            cmbReward1Score.DataSource = Utility.GetEnumDataSource<CompletionReq>();
            cmbReward2Diff.DataSource = Utility.GetEnumDataSource<SupportedDifficulty>();
            cmbReward2Score.DataSource = Utility.GetEnumDataSource<CompletionReq>();
            // Song Pool controls
            nudAmountInPool.ValueChanged += (s, e) => SavePoolValues();
            nudPoolMinDifficulty.ValueChanged += (s, e) => SavePoolValues();
            nudPoolMaxDifficulty.ValueChanged += (s, e) => SavePoolValues();
            cmbReward1Diff.SelectedIndexChanged += (s, e) => SavePoolValues();
            cmbReward2Diff.SelectedIndexChanged += (s, e) => SavePoolValues();
            cmbReward1Score.SelectedIndexChanged += (s, e) => SavePoolValues();
            cmbReward2Score.SelectedIndexChanged += (s, e) => SavePoolValues();

            btnListValidSongs.Click += ListValidSongs;

            lbSongPoolList.DataSource = YAML.YAYARG.song_pools.Select(x => new SongPoolContainer(x.Key, x.Value)).ToArray();
            lbSongPoolList.SelectedIndexChanged += (s, e) => 
            { 
                SelectedSongPool = lbSongPoolList.SelectedItem is SongPoolContainer kvp ? kvp : null;  
                LoadSongPool();
            };
            txtNewPoolIsntrument.DataSource = Utility.GetEnumDataSource<SupportedInstrument>();

            lbActiveSongs.ItemCheck += (s, e) => ToggleGlobalExludeList(((TaggedSongExportExtendedData)lbActiveSongs.Items[e.Index]).core, e.NewValue);
            lbActiveSongs.SelectedIndexChanged += (s, e) => UpdateIncludeExcludeListOnSongPage();

            btnEditExcludePools.Click += (s, e) => EditExculdeIncludeDictForSong(YAML.YAYARG.exclusions_per_pool, "Exclude");
            btnEditIncludePools.Click += (s, e) => EditExculdeIncludeDictForSong(YAML.YAYARG.inclusions_per_pool, "Include");

            lbActiveSongs.MouseDown += LbActiveSongs_MouseDown;

            btnExport.Click += (s, e) =>
            {
                ctxMenu.Items.Clear();
                ctxMenu.Items.Add("Export as Text File", null, (_, __) => SaveSongData(true));
                ctxMenu.Items.Add("Export as Json File", null, (_, __) => SaveSongData(true));
                ctxMenu.Show(btnExport, new Point(0, btnExport.Height));
            };

            btnGenYaml.Click += SaveYaml;
        }

        private void ListValidSongs(object sender, EventArgs e)
        {
            if (IsLoadingNewSongPool || SelectedSongPool == null)
                return;
            var Form = new ValueSelectForm($"Valid Songs for Pool {SelectedSongPool.Name}", false);
            Form.SetItems(ExportFile.Where(x => x.core.ValidForPool(SelectedSongPool.Pool)).OrderBy(x => x.ToString()), x => x.ToString());
            Form.ShowDialog();
        }

        private void LbActiveSongs_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
                return;

            int index = lbActiveSongs.IndexFromPoint(e.Location);
            if (index == ListBox.NoMatches)
                return;

            lbActiveSongs.SelectedIndex = index;

            var item = (TaggedSongExportExtendedData)lbActiveSongs.Items[index];

            ctxMenu.Items.Clear();
            ctxMenu.Items.Add("Copy song hash", null, (_, __) =>
            {
                Clipboard.SetText(item.core.SongChecksum);
            });

            ctxMenu.Show(lbActiveSongs, e.Location);
        }

        private void SaveSongData(bool AsHash)
        {
            var songDict = ExportFile.ToDictionary(x => x.core.SongChecksum, x => x.Compress());
            var ToSave = AsHash ? SongDataConverter.ConvertSongDataToBase64(ExportFile) : JsonConvert.SerializeObject(songDict, Formatting.Indented);
            var ext = AsHash ? "txt" : "json";

            using (var dialog = new SaveFileDialog())
            {
                dialog.InitialDirectory = OutputFolder;
                dialog.FileName = $"SongData.{ext}";
                dialog.Filter = $"{ext.ToUpper()} files (*.{ext})|*.{ext}|All files (*.*)|*.*";
                dialog.DefaultExt = ext;
                dialog.Title = $"Save Song Data as {(AsHash ? "Hash String" : "Export File")}";

                if (dialog.ShowDialog() == DialogResult.OK)
                    File.WriteAllText(dialog.FileName, ToSave);
            }
        }

        private void SaveYaml(object sender, EventArgs e)
        {
            if (String.IsNullOrWhiteSpace(YAML.name))
                MessageBox.Show("You must enter a slot name!");
            else if (YAML.YAYARG.song_pools.Count < 1)
                MessageBox.Show("You must create at least one song pool! Go to the song pool tab to create one!");
            else if (YAML.YAYARG.song_pools.Select(x => x.Value.amount_in_pool).Sum() < YAML.YAYARG.starting_songs + 1)
                MessageBox.Show("Not enough songs in your song pools. Add more songs to your pools");
            else
            {
                ValidateIncludeExcludeList();
                using (SaveFileDialog saveDialog = new SaveFileDialog())
                {
                    saveDialog.InitialDirectory = OutputFolder;
                    saveDialog.FileName = $"{YAML.name}_YAYARG.yaml";
                    saveDialog.Filter = "YAML files (*.yaml)|*.yaml|All files (*.*)|*.*";
                    saveDialog.DefaultExt = "yaml";
                    saveDialog.Title = "Save YAYARG YAML File";

                    if (saveDialog.ShowDialog() == DialogResult.OK)
                        YAMLWriter.WriteToFile(YAML, saveDialog.FileName);
                }
            }
        }
        private void SongPoolListUpdated()
        {
            var AllPools = YAML.YAYARG.song_pools.Select(x => new SongPoolContainer(x.Key, x.Value)).ToArray();
            lbSongPoolList.DataSource = null;
            lbSongPoolList.DataSource = AllPools;
        }

        private void RegenerateGoalPoolList()
        {
            var AllPools = YAML.YAYARG.song_pools.Select(x => new SongPoolContainer(x.Key, x.Value)).ToArray();
            var CurrentGoalPool = chkGoalPoolPlando.Checked ? cmbGoalPoolPlando.SelectedItem?.ToString() : null;
            cmbGoalPoolPlando.DataSource = AllPools.Select(x => x.Name).ToArray();

            if (CurrentGoalPool != null)
            {
                var matchingPool = cmbGoalPoolPlando.Items.Cast<string>().FirstOrDefault(x => x == CurrentGoalPool);
                cmbGoalPoolPlando.SelectedItem = matchingPool;
            }
            else
                cmbGoalPoolPlando.SelectedItem = null;
        }

        private void RegenerateGoalSongList()
        {
            var CurrentPoolPlando = YAML.YAYARG.goal_pool_plando != null && YAML.YAYARG.song_pools.TryGetValue(YAML.YAYARG.goal_pool_plando, out var pool) ? pool : null;
            var CurrentGoalSong = chkGoalSongPlando.Checked && cmbGoalSongPlando.SelectedItem is DisplayItem<SongExportExtendedData> selectedSong
                ? selectedSong.Value.core.SongChecksum
                : null;

            var ValidSongs = ExportFile;
            if (CurrentPoolPlando != null)
                ValidSongs = ExportFile.Where(x => x.core.ValidForPool(CurrentPoolPlando)).ToArray();

            cmbGoalSongPlando.DataSource = Utility.GetDataSource<SongExportExtendedData>(ValidSongs, x => $"{x.core.Name} by {x.core.Artist}");

            if (CurrentGoalSong != null)
            {
                var matchingItem = cmbGoalSongPlando.Items.Cast<DisplayItem<SongExportExtendedData>>().FirstOrDefault(x => x.Value.core.SongChecksum == CurrentGoalSong);
                cmbGoalSongPlando.SelectedItem = matchingItem;
            }
            else
                cmbGoalSongPlando.SelectedItem = null;
        }

        public class SongPoolContainer
        {
            public SongPoolContainer(string name, SongPool pool) { Name = name; Pool = pool; }
            public string Name;
            public SongPool Pool;
            public override string ToString()
            {
                return $"{Name} [{Pool.instrument.GetDescription()}]";
            }
        }

        private void btnAddPool_Click(object sender, EventArgs e)
        {
            var instrument = txtNewPoolIsntrument.SelectedItem is DisplayItem<SupportedInstrument> inst ? inst : null;
            if (string.IsNullOrWhiteSpace(txtNewPoolName.Text))
                MessageBox.Show("Pool Name must not be blank!");
            else if (YAML.YAYARG.song_pools.ContainsKey(txtNewPoolName.Text))
                MessageBox.Show($"There is already a pool with the name {txtNewPoolName.Text}");
            else if (instrument is null)
                MessageBox.Show($"Please select a valid Instrument");
            else
            {
                var Name = txtNewPoolName.Text.Trim();
                YAML.YAYARG.song_pools.Add(Name, Utility.NewSongPool(instrument.Value));
                SongPoolListUpdated();
                RegenerateGoalPoolList();
                lbSongPoolList.SelectedItem = lbSongPoolList.Items.Cast<SongPoolContainer>()?.FirstOrDefault(x => x.Name == Name);
            }
        }

        private void btnRemovePool_Click(object sender, EventArgs e)
        {
            if (SelectedSongPool is null)
                return;
            YAML.YAYARG.song_pools.Remove(SelectedSongPool.Name);
            SongPoolListUpdated();
            RegenerateGoalPoolList();
            RegenerateGoalSongList();
        }

        private void UpdateIncludeExcludeListOnSongPage()
        {
            txtPoolInclude.Text = "";
            txtPoolExclude.Text = "";
            btnEditExcludePools.Enabled = false;
            btnEditIncludePools.Enabled = false;
            SongExportExtendedData ExtendedData = lbActiveSongs.SelectedItem is SongExportExtendedData ed ? ed : null;
            if (ExtendedData is null)
                return;

            if (YAML.YAYARG.exclusions_per_pool.TryGetValue(ExtendedData.core.SongChecksum, out var exList))
                txtPoolExclude.Text = string.Join(", ", exList);
            if (YAML.YAYARG.inclusions_per_pool.TryGetValue(ExtendedData.core.SongChecksum, out var incList))
                txtPoolInclude.Text = string.Join(", ", incList);
            btnEditExcludePools.Enabled = true;
            btnEditIncludePools.Enabled = true;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            ValidateIncludeExcludeList();
            YAML.YAYARG.songList = string.Empty;
            File.WriteAllText(cache, JsonConvert.SerializeObject(YAML));
        }
        private void LoadYamlToControls()
        {
            txtSlotName.Text = YAML.name??"";

            // Song Check Settings
            nudSongExtra.Value = YAML.YAYARG.song_check_extra;
            nudSongPack.Value = YAML.YAYARG.song_pack_size;
            nudStartingSongs.Value = YAML.YAYARG.starting_songs;
            chkReuseSongs.Checked = YAML.YAYARG.reuse_songs;
            chkInstrumentShuffle.Checked = YAML.YAYARG.instrument_shuffle;

            // Goal Song Settings
            nudGoalFame.Value = YAML.YAYARG.fame_point_needed;
            nudGoalSetlist.Value = YAML.YAYARG.setlist_needed;
            nudFameAmount.Value = YAML.YAYARG.fame_point_amount;
            chkGoalItemNeeded.Checked = YAML.YAYARG.goal_song_item_needed;

            // Goal Plando
            chkGoalPoolPlando.Checked = !string.IsNullOrEmpty(YAML.YAYARG.goal_pool_plando);
            chkGoalSongPlando.Checked = !string.IsNullOrEmpty(YAML.YAYARG.goal_song_plando);

            // Filler Items
            nudStarPower.Value = YAML.YAYARG.star_power;
            nudSwapPick.Value = YAML.YAYARG.swap_song_choice;
            nudSwapRandom.Value = YAML.YAYARG.swap_song_random;
            nudLowerDiff.Value = YAML.YAYARG.lower_difficulty;
            nudRestartTrap.Value = YAML.YAYARG.restart_trap;
            nudRockTrap.Value = YAML.YAYARG.rock_meter_trap;
            nudNothingItem.Value = YAML.YAYARG.nothing;
            nudFailPrevention.Value = YAML.YAYARG.fail_prevention;
        }

        private void ValidateIncludeExcludeList()
        {
            validate(YAML.YAYARG.exclusions_per_pool);
            validate(YAML.YAYARG.inclusions_per_pool);
            void validate(Dictionary<string, string[]> Target)
            {
                foreach (var hash in Target.Keys.ToArray())
                {
                    var Song = ExportFile.FirstOrDefault(x => x.core.SongChecksum == hash);
                    if (Song is null)
                    {
                        Target.Remove(hash);
                        continue;
                    }
                    HashSet<string> InvalidPools = new HashSet<string>();
                    foreach(var pool in Target[hash].ToArray())
                    {
                        if (!YAML.YAYARG.song_pools.TryGetValue(pool, out var poolData))
                            InvalidPools.Add(pool);
                        else if (!Song.core.ValidForPool(poolData))
                            InvalidPools.Add(pool);
                    }
                    Target[hash] = Target[hash].Where(x => !InvalidPools.Contains(x)).ToArray();
                }
            }
        }

        private void EditExculdeIncludeDictForSong(Dictionary<string, string[]> Target, string Action)
        {
            SongExportExtendedData ExtendedData = lbActiveSongs.SelectedItem is SongExportExtendedData ed ? ed : null;
            if (ExtendedData is null)
                return;

            ValueSelectForm form = new ValueSelectForm($"Select pools to {Action} {ExtendedData.core.Name} by {ExtendedData.core.Artist}");
            var allPools = YAML.YAYARG.song_pools.Where(x => ExtendedData.core.TryGetDifficulty(x.Value.instrument, out _)).Select(x => x.Key);
            var currentlySelected = Target.TryGetValue(ExtendedData.core.SongChecksum, out var cur) ? cur : Array.Empty<string>(); ;
            form.SetItems<string>(allPools, x => x, currentlySelected);

            if (form.ShowDialog() == DialogResult.OK)
            {
                var Selected = form.GetSelectedValues<string>().ToArray();
                if (Selected.Length > 0)
                    Target[ExtendedData.core.SongChecksum] = form.GetSelectedValues<string>().ToArray();
                else
                    Target.Remove(ExtendedData.core.SongChecksum);
                UpdateIncludeExcludeListOnSongPage();
            }
        }

        private void ToggleGlobalExludeList(SongExportData item, CheckState CheckState)
        {
            if (PrintingSongs)
                return;

            if (CheckState != CheckState.Checked)
                YAML.YAYARG.song_exclusion_list.Add(item.SongChecksum);
            else
                YAML.YAYARG.song_exclusion_list.Remove(item.SongChecksum);
        }

        private enum DisplayTypes
        {
            Hash,
            Name,
            Artist,
            Album,
            Source,
            Charter,
            Genre
        }
        HashSet<DisplayTypes> CurrentTypes = new HashSet<DisplayTypes>() { DisplayTypes.Name, DisplayTypes.Artist };
        private void btnFilter_Click(object sender, EventArgs e)
        {
            ValueSelectForm form = new ValueSelectForm($"Select Extra data to show");
            var enums = Utility.GetEnumDataSource<DisplayTypes>();
            form.SetItems(enums, CurrentTypes);
            if (form.ShowDialog() == DialogResult.OK)
            {
                CurrentTypes = form.GetSelectedValues<DisplayTypes>().ToHashSet();
                PrintActiveSongs(sender, e);
            }
        }
    }
}
