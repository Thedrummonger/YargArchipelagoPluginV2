using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Forms;
using YargArchipelagoPlugin;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ProgressBar;
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
            this.lbActiveSongs.Columns.Clear();
            this.lbActiveSongs.Columns.Add(new DataGridViewCheckBoxColumn { Width = 30 });
            this.lbActiveSongs.Columns.Add(new DataGridViewTextBoxColumn { AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            this.lbActiveSongs.ReadOnly = false;
            this.lbActiveSongs.Columns[0].ReadOnly = false;
            this.lbActiveSongs.Columns[1].ReadOnly = true;
            this.lbActiveSongs.EditMode = DataGridViewEditMode.EditOnEnter;
            this.lbActiveSongs.AllowUserToResizeRows = false;
            this.lbActiveSongs.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing;
            this.lbActiveSongs.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            InitializeClientComponents();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            if (CrossPlatformFileLoader.IsRunningUnderProton())
            {
                var result = MessageBox.Show(
                    "This application is not supported under Proton.\n\n" +
                    "It is recommended to run it using Wine or Bottles instead.\n\n" +
                    "If you continue, you must manually copy your SongExport.json\n" +
                    $"to the AppData/{CrossPlatformFileLoader.RootFolderName} folder inside the Proton prefix.\n\n" +
                    "Do you want to continue anyway?",
                    "Unsupported Environment",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2);

                if (result != DialogResult.Yes)
                {
                    Close();
                    return;
                }
            }

            var path = CrossPlatformFileLoader.TryGetSongExportJson();

            if (path == null)
            {
                MessageBox.Show(
                    "ERROR: Song data file could not be found.\n\n" +
                    "Ensure you have launched YARG at least once with the mod loaded.",
                    "File Not Found",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                Close();
                return;
            }

            var datas = Utility.TryParseSongExport(path, out string error);
            if (datas is null)
            {
                MessageBox.Show(
                    "ERROR: Song data file could not be parsed.\n\n" +
                    $"File: {path}\n\n" +
                    $"Error: {error}\n\n" +
                    "The file may be corrupted. Try launching YARG again to regenerate it.",
                    "Parse Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                Close();
                return;
            }

            ExportFile = datas.Select(x => new SongExportExtendedData(x)).ToArray();
            SanitizeExportFile();
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

        private void SanitizeExportFile()
        {
            for (var i = 0; i < ExportFile.Length; i++)
            {
                var diffs = ExportFile[i].core.Difficulties;
                foreach (var intensity in diffs.Keys.ToList())
                    diffs[intensity] = Utility.Clamp(diffs[intensity], 0, int.MaxValue);
            }
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
                nudPoolRandomVariance.Value = 0;
                nudPoolMinDifficulty .Value = 0;
                nudPoolMaxDifficulty .Value = 0;
                nudPoolMinTime.Value = 0;
                nudPoolMaxTime.Value = 0;
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
            nudPoolRandomVariance.Value = SelectedSongPool.Pool.random_variance;
            nudPoolMinDifficulty.Value = SelectedSongPool.Pool.min_difficulty;
            nudPoolMaxDifficulty.Value = SelectedSongPool.Pool.max_difficulty;
            if (SelectedSongPool.Pool.min_time <= 0 && SelectedSongPool.Pool.max_time <= 0)
                SelectedSongPool.Pool.max_time = 3600;
            nudPoolMinTime.Value = SelectedSongPool.Pool.min_time;
            nudPoolMaxTime.Value = SelectedSongPool.Pool.max_time;
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
            SelectedSongPool.Pool.random_variance = (int)nudPoolRandomVariance.Value;
            SelectedSongPool.Pool.min_difficulty = (int)nudPoolMinDifficulty.Value;
            SelectedSongPool.Pool.max_difficulty = (int)nudPoolMaxDifficulty.Value;
            SelectedSongPool.Pool.min_time = (int)nudPoolMinTime.Value;
            SelectedSongPool.Pool.max_time = (int)nudPoolMaxTime.Value;
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
            Dictionary<SongExportExtendedData, string> DisplayString = ExportFile.ToDictionary(x => x, x => AddTags(x));
            var ActiveSongs = FormHelpers.FilterItems(ExportFile, txtActiveSongFilter.Text, x => DisplayString[x]);
            ActiveSongs = ActiveSongs.OrderBy(x => x.ToString()).ToArray();
            lbActiveSongs.Rows.Clear();
            foreach (var d in ActiveSongs)
            {
                int r = lbActiveSongs.Rows.Add(!IsExcluded(d), DisplayString[d]);
                lbActiveSongs.Rows[r].Tag = d;
            }

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
                if (YAML.YAYARG.inclusions_per_pool.ContainsKey(extendedData.core.SongChecksum) ||
                    YAML.YAYARG.exclusions_per_pool.ContainsKey(extendedData.core.SongChecksum))
                    final += " *";
                return string.IsNullOrWhiteSpace(final) ? extendedData.core.SongChecksum : final;
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
            nudSongPackPercentage.ValueChanged += (s, e) =>
            {
                YAML.YAYARG.song_pack_percentage = (int)nudSongPackPercentage.Value;
                nudSongPack.Enabled = YAML.YAYARG.song_pack_percentage > 0;
            };
            nudUnlockExtra.ValueChanged += (s, e) => YAML.YAYARG.extra_song_unlock = (int)nudUnlockExtra.Value;
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
            nudNothingItem.ValueChanged += (s, e) => YAML.YAYARG.nothing_item = (int)nudNothingItem.Value;
            nudFailPrevention.ValueChanged += (s, e) => YAML.YAYARG.fail_prevention = (int)nudFailPrevention.Value;
            //Song Pools
            cmbReward1Diff.DataSource = Utility.GetEnumDataSource<SupportedDifficulty>();
            cmbReward1Score.DataSource = Utility.GetEnumDataSource<CompletionReq>();
            cmbReward2Diff.DataSource = Utility.GetEnumDataSource<SupportedDifficulty>();
            cmbReward2Score.DataSource = Utility.GetEnumDataSource<CompletionReq>();
            // Song Pool controls
            nudAmountInPool.ValueChanged += (s, e) => SavePoolValues();
            nudPoolRandomVariance.ValueChanged += (s, e) => SavePoolValues();
            nudPoolMinDifficulty.ValueChanged += (s, e) => SavePoolValues();
            nudPoolMaxDifficulty.ValueChanged += (s, e) => SavePoolValues();
            nudPoolMinTime.ValueChanged += (s, e) => SavePoolValues();
            nudPoolMaxTime.ValueChanged += (s, e) => SavePoolValues();
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
            lbActiveSongs.CurrentCellDirtyStateChanged += (s, e) => {
                if (lbActiveSongs.IsCurrentCellDirty) lbActiveSongs.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };

            lbActiveSongs.CellValueChanged += (s, e) => {
                if (e.RowIndex < 0 || e.ColumnIndex != 0) return;
                var d = (SongExportExtendedData)lbActiveSongs.Rows[e.RowIndex].Tag;
                ToggleGlobalExludeList(d.core, (bool)lbActiveSongs.Rows[e.RowIndex].Cells[0].Value ? CheckState.Checked : CheckState.Unchecked);
            };
            lbActiveSongs.SelectionChanged += (s, e) => UpdateIncludeExcludeListOnSongPage();
            lbActiveSongs.KeyDown += (s, e) => {
                if ((e.KeyCode != Keys.Space && e.KeyCode != Keys.Enter) || lbActiveSongs.SelectedRows.Count == 0) return;
                bool anyUnchecked = false;
                foreach (DataGridViewRow r in lbActiveSongs.SelectedRows)
                    if (!(bool)r.Cells[0].Value) { anyUnchecked = true; break; }
                foreach (DataGridViewRow r in lbActiveSongs.SelectedRows)
                    r.Cells[0].Value = anyUnchecked;
                e.Handled = true;
            };
            lbActiveSongs.CellDoubleClick += (s, e) => {
                if (e.RowIndex < 0 || e.ColumnIndex != 1) return;
                var cell = lbActiveSongs.Rows[e.RowIndex].Cells[0];
                cell.Value = !(bool)cell.Value;
            };

            btnEditExcludePools.Click += (s, e) => EditExculdeIncludeDictForSong(YAML.YAYARG.exclusions_per_pool, "Exclude");
            btnEditIncludePools.Click += (s, e) => EditExculdeIncludeDictForSong(YAML.YAYARG.inclusions_per_pool, "Include");

            lbActiveSongs.MouseDown += LbActiveSongs_MouseDown;

            btnExport.Click += (s, e) =>
            {
                ctxMenu.Items.Clear();
                ctxMenu.Items.Add("Export as Text File", null, (_, __) => SaveSongData(true));
                ctxMenu.Items.Add("Export as Json File", null, (_, __) => SaveSongData(false));
                ctxMenu.Show(btnExport, new Point(0, btnExport.Height));
            };

            btnGenYaml.Click += (_, __) => GenerateYamlClick(SongDataSaveType.compressed);
            btnGenYaml.MouseDown += GenerateRightClick;

            btnSeedStats.Click += ShowSeedStats;
        }

        private void ShowSeedStats(object sender, EventArgs e)
        {
            var lines = new List<string>();
            var validInSeed = new HashSet<SongExportExtendedData>();
            int totalSongCount = 0;
            double totalWorstCaseTime = 0;

            foreach (var pool in YAML.YAYARG.song_pools)
            {
                totalSongCount += (int)pool.Value.amount_in_pool;

                var songs = ExportFile.Where(x => x.core.ValidForPool(pool.Value));

                if (!songs.Any())
                    continue;

                foreach (var song in songs)
                    validInSeed.Add(song);

                lines.Add(null);
                lines.Add($"Pool [{pool.Key}] Stats");

                var (count, avg, _) = GetLengthStats(songs);
                var playTime = FormatSeconds(avg * pool.Value.amount_in_pool);

                double worstCaseTime = GetWorstCaseTime(songs, (int)pool.Value.amount_in_pool + pool.Value.random_variance);
                totalWorstCaseTime += worstCaseTime;

                lines.Add($"{"Songs in pool",-20} {pool.Value.amount_in_pool}");
                lines.Add($"{"Valid candidates",-20} {count}");
                lines.Add($"{"Avg song length",-20} {FormatSeconds(avg)}");
                lines.Add($"{"Est. play time",-20} {playTime}");
                lines.Add($"{"Worst case time",-20} {FormatSeconds(worstCaseTime)}");
                lines.Add("");
            }

            var (allCount, allAvg, _) = GetLengthStats(validInSeed);
            var allPlay = FormatSeconds(allAvg * totalSongCount);

            lines.Insert(0, "");
            lines.Insert(0, $"{"Worst case time",-20} {FormatSeconds(totalWorstCaseTime)}");
            lines.Insert(0, $"{"Est. play time",-20} {allPlay}");
            lines.Insert(0, $"{"Avg song length",-20} {FormatSeconds(allAvg)}");
            lines.Insert(0, $"{"Valid candidates",-20} {allCount}");
            lines.Insert(0, $"{"Songs in seed",-20} {totalSongCount}");
            lines.Insert(0, $"Overall Seed Stats");

            this.ShowTextDialog("Seed Stats", lines);
        }

        private double GetWorstCaseTime(IEnumerable<SongExportExtendedData> songs, int countNeeded)
        {
            return songs.OrderByDescending(x => x.core.Time).Take(countNeeded).Sum(x => x.core.Time);
        }

        private string FormatSeconds(double seconds)
        {
            var ts = TimeSpan.FromSeconds(seconds);

            int hours = ts.Hours + (ts.Days * 24);
            int minutes = ts.Minutes;
            int secs = ts.Seconds;

            var parts = new List<string>();

            if (hours > 0)
                parts.Add($"{hours} hour{(hours == 1 ? "" : "s")}");

            if (minutes > 0)
                parts.Add($"{minutes} minute{(minutes == 1 ? "" : "s")}");

            if (secs > 0 || parts.Count == 0)
                parts.Add($"{secs} second{(secs == 1 ? "" : "s")}");

            return string.Join(", ", parts);
        }

        private (int Count, double AverageTime, double TotalTime) GetLengthStats(IEnumerable<SongExportExtendedData> songs)
        {
            int Count = 0;
            double TotalTime = 0;
            foreach(var s in songs)
            {
                Count++;
                TotalTime += s.core.Time;
            }
            double AverageTime = Count == 0 ? 0 : TotalTime / (double)Count;
            return (Count, AverageTime, TotalTime);
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
            var h = lbActiveSongs.HitTest(e.X, e.Y);
            if (h.RowIndex < 0) 
                return;
            lbActiveSongs.ClearSelection();
            lbActiveSongs.Rows[h.RowIndex].Selected = true;
            var item = (SongExportExtendedData)lbActiveSongs.Rows[h.RowIndex].Tag;
            ctxMenu.Items.Clear();
            ctxMenu.Items.Add("Copy song hash", null, (_, __) => Clipboard.SetText(item.core.SongChecksum));
            ctxMenu.Show(lbActiveSongs, new Point(e.X, e.Y));
        }

        public void GenerateRightClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
                return;

            ctxMenu.Items.Clear();
            ctxMenu.Items.Add("Generate Uncompressed", null, (_, __) => GenerateYamlClick(SongDataSaveType.standard));
            ctxMenu.Items.Add("Generate With Song Json", null, (_, __) => GenerateYamlClick(SongDataSaveType.file));
            ctxMenu.Items.Add("Generate With Compressed Song Json", null, (_, __) => GenerateYamlClick(SongDataSaveType.fileCompressed));
            ctxMenu.Show(btnGenYaml, new Point(e.X, e.Y));

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

        private enum SongDataSaveType { standard, compressed, file, fileCompressed }

        private const int MaxRecommendedSongs = 500;
        private void GenerateYamlClick(SongDataSaveType type)
        {
            if (String.IsNullOrWhiteSpace(YAML.name))
            {
                MessageBox.Show("You must enter a slot name!");
                return;
            }
            else if (YAML.YAYARG.song_pools.Count < 1)
            {
                MessageBox.Show("You must create at least one song pool! Go to the song pool tab to create one!");
                return;
            }
            else if (YAML.YAYARG.song_pools.Select(x => x.Value.amount_in_pool).Sum() < YAML.YAYARG.starting_songs + 1)
            {
                MessageBox.Show("Not enough songs in your song pools. Add more songs to your pools");
                return;
            }
            ValidateIncludeExcludeList();
            var ExportedSongList = ExportFile;

            SongDistributor distributor = new SongDistributor().WithAvailableSongs(ExportFile.Where(x => !YAML.YAYARG.song_exclusion_list.Contains(x.core.SongChecksum)))
                .WithReuseSongs(YAML.YAYARG.reuse_songs).WithInclusionLists(YAML.YAYARG.inclusions_per_pool).WithExclusionLists(YAML.YAYARG.exclusions_per_pool)
                .WithPools(YAML.YAYARG.song_pools).WithGoalSong(YAML.YAYARG.goal_song_plando, YAML.YAYARG.goal_pool_plando);


            if (ExportedSongList.Length > MaxRecommendedSongs && (type == SongDataSaveType.compressed || type == SongDataSaveType.fileCompressed))
                RemoveUnneededSongs(out ExportedSongList);

            if (ExportedSongList.Length > MaxRecommendedSongs && (type == SongDataSaveType.compressed || type == SongDataSaveType.fileCompressed) && 
                distributor.CreateTrimmedSetlistforYAML(out var trim))
                    ExportedSongList = trim;

            if (ExportedSongList.Length > MaxRecommendedSongs)
            {
                var Confirmation = MessageBox.Show(
                    $"Your seed contains {ExportedSongList.Length} valid songs, which exceeds the recommended maximum of {MaxRecommendedSongs}.\n\n" +
                    $"Very large song lists can create excessively large YAML files and data packets. This may cause generation failures, " +
                    $"connection issues, or instability; especially in large multiworld games or sessions hosted on the Archipelago website.\n\n" +
                    $"It is strongly recommended that you reduce your song list to a smaller, AP-focused setlist.\n\n" +
                    $"Click OK to proceed and accept these risks, or Cancel to go back.",
                    "MAX SONG LIMIT EXCEEDED",
                    MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Warning);
                if (Confirmation != DialogResult.OK)
                    return;
            }

            string DataFilePath = string.Empty;
            if (type == SongDataSaveType.file || type == SongDataSaveType.fileCompressed)
            {
                var songDict = ExportedSongList.ToDictionary(x => x.core.SongChecksum, x => x.Compress());
                string Json = JsonConvert.SerializeObject(songDict, Formatting.Indented);
                using (SaveFileDialog saveDialog = new SaveFileDialog())
                {
                    saveDialog.InitialDirectory = OutputFolder;
                    saveDialog.FileName = $"{YAML.name}_song_export.json";
                    saveDialog.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*";
                    saveDialog.DefaultExt = "json";
                    saveDialog.Title = "Save Song Export File";

                    if (saveDialog.ShowDialog() == DialogResult.OK)
                    {
                        DataFilePath = saveDialog.FileName;
                        File.WriteAllText(saveDialog.FileName, Json);
                    }
                    else
                        return;
                }
            }

            YAML.YAYARG.songList = string.IsNullOrWhiteSpace(DataFilePath) ? SongDataConverter.ConvertSongDataToBase64(ExportedSongList) : Path.GetFileName(DataFilePath);

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
        

        private void RemoveUnneededSongs(out SongExportExtendedData[] UsedSongs)
        {
            UsedSongs = ExportFile.Where(IsUsableBySeed).ToArray();

            bool IsUsableBySeed(SongExportExtendedData song)
            {
                if (YAML.YAYARG.goal_song_plando == song.core.SongChecksum)
                    return true;
                if (YAML.YAYARG.inclusions_per_pool.Any(x => x.Value.Contains(song.core.SongChecksum)))
                    return true;
                if (YAML.YAYARG.song_exclusion_list.Contains(song.core.SongChecksum))
                    return false;
                return YAML.YAYARG.song_pools.Values.Any(p => song.core.ValidForPool(p));
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
            public SongPoolContainer(string name, YAMLSongPool pool) { Name = name; Pool = pool; }
            public string Name;
            public YAMLSongPool Pool;
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
            if (lbActiveSongs.SelectedRows.Count != 1) return;
            SongExportExtendedData ExtendedData = (SongExportExtendedData)lbActiveSongs.SelectedRows[0].Tag;
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
            nudSongPack.Value = Utility.Clamp(YAML.YAYARG.song_pack_size, 2, 999) ;
            nudSongPackPercentage.Value = YAML.YAYARG.song_pack_percentage;
            nudSongPack.Enabled = YAML.YAYARG.song_pack_percentage > 0;
            nudStartingSongs.Value = YAML.YAYARG.starting_songs;
            nudUnlockExtra.Value = YAML.YAYARG.extra_song_unlock;
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
            nudNothingItem.Value = YAML.YAYARG.nothing_item;
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
                        else if (!Song.core.TryGetDifficulty(poolData.instrument, out _))
                            InvalidPools.Add(pool);
                    }
                    var NewValues = Target[hash].Where(x => !InvalidPools.Contains(x)).ToArray();
                    if (!NewValues.Any())
                        Target.Remove(hash);
                    else
                        Target[hash] = NewValues;
                }
            }
        }

        private void EditExculdeIncludeDictForSong(Dictionary<string, string[]> Target, string Action)
        {
            if (lbActiveSongs.SelectedRows.Count != 1) return;
            SongExportExtendedData ExtendedData = (SongExportExtendedData)lbActiveSongs.SelectedRows[0].Tag;

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
                FormHelpers.ClearFilters();
            }
        }
    }
}
