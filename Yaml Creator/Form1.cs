using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using YargArchipelagoPlugin;
using static Yaml_Creator.SongData;
using static Yaml_Creator.Utility;
using static YargArchipelagoCommon.CommonData;

namespace Yaml_Creator
{
    public partial class Form1 : Form
    {
        public static SongExportExtendedData[] ExportFile;
        public static YAMLCore YAML = new YAMLCore();
        public static string OutputFolder = Path.Combine(Application.StartupPath, "Output");
        public static SongPoolContainer SelectedSongPool = null;
        public bool IsLoadingNewSongPool = false;
        public Form1()
        {
            InitializeComponent();
            CreateListeners();
            Directory.CreateDirectory(OutputFolder);
        }

        private void LoadSongPool()
        {
            IsLoadingNewSongPool = true;
            if (SelectedSongPool == null)
            {
                gbSelectedPool.Enabled = false;
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
        }

        private SongExportData[] LoadSongData(string SongExportFile)
        {
            if (SongExportFile is null || !File.Exists(SongExportFile))
                return null;
            var RawData = File.ReadAllText(SongExportFile);
            try
            {
                return JsonConvert.DeserializeObject<SongExportData[]>(RawData);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return null;
            }
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
            cmbGoalPoolPlando.Enabled = chkGoalPoolPlando.Checked;
            cmbGoalSongPlando.Enabled = chkGoalSongPlando.Checked;
        }

        public void PrintActiveSongs(object sender, EventArgs e)
        {
            var ActiveSongs = FormHelpers.FilterItems(ExportFile, txtActiveSongFilter.Text, x => x.ToString());
            ActiveSongs = ActiveSongs.OrderBy(x => x.ToString()).ToArray();
            lbActiveSongs.DataSource = ActiveSongs;
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

            lbSongPoolList.DataSource = YAML.YAYARG.song_pools.Select(x => new SongPoolContainer(x.Key, x.Value)).ToArray();
            lbSongPoolList.SelectedIndexChanged += (s, e) => 
            { 
                SelectedSongPool = lbSongPoolList.SelectedItem is SongPoolContainer kvp ? kvp : null;  
                LoadSongPool();
            };
            txtNewPoolIsntrument.DataSource = Utility.GetEnumDataSource<SupportedInstrument>();

            btnGenYaml.Click += SaveYaml;
        }

        private void SaveYaml(object sender, EventArgs e)
        {
            if (String.IsNullOrWhiteSpace(YAML.name))
                MessageBox.Show("You must enter a slot name!");
            else if (YAML.YAYARG.song_pools.Count < 1)
                MessageBox.Show("You must create at least one song pool! Go to the song pool tab to create one!");
            else
            {
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
                ValidSongs = ExportFile.Where(x => x.core.TryGetDifficulty(CurrentPoolPlando.instrument, out var diff) && diff <= CurrentPoolPlando.max_difficulty && diff >= CurrentPoolPlando.min_difficulty)
                    .ToArray();

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
    }
}
