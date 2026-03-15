using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AscianMusicPlayer.Audio;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace AscianMusicPlayer.Windows
{
    public class MainWindow : Window
    {
        private enum RepeatMode
        {
            Off,
            All,
            One
        }

        private readonly Plugin _plugin;
        private string _mediaFolder = string.Empty;
        private List<Song> _songs = new();
        private List<Song> _displaySongs = new();
        private Song? _currentSong;
        private int _selectedSongIndex = -1;
        private bool _isShuffle = false;
        private RepeatMode _repeatMode = RepeatMode.Off;
        private List<int> _shuffleQueue = new();
        private int _shufflePosition = -1;
        private readonly Random _random = new();
        private int _tableResetCounter = 1;
        private ImGuiTableColumnSortSpecsPtr _lastSortSpecs;
        private bool _sortDirty = true;
        private int _lastShuffleCount = 0;
        private bool _skipNextColumnSave = false;
        private Guid? _activePlaylistId;
        private string _searchTitle = string.Empty;
        private string _searchArtist = string.Empty;
        private string _searchAlbum = string.Empty;
        private List<Song> _filteredSongs = new();

        public MainWindow(Plugin plugin) : base("Ascian Music Player###AscianMusicPlayer")
        {
            this._plugin = plugin;
            this.Size = new Vector2(500, 400);
            this.SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(400, 300),
                MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
            };

            this.SizeCondition = ImGuiCond.FirstUseEver;

            this.Flags |= ImGuiWindowFlags.MenuBar;

            _mediaFolder = Plugin.Settings.MediaFolder;
            _displaySongs = _songs;

            _plugin.AudioController.SongEnded += OnSongEnded;
        }

        public void Cleanup()
        {
            _plugin.AudioController.SongEnded -= OnSongEnded;
        }

        private void OnSongEnded(object? sender, EventArgs e)
        {
            switch (_repeatMode)
            {
                case RepeatMode.One:
                    if (_currentSong != null)
                    {
                        PlaySong(_currentSong);
                    }
                    break;

                case RepeatMode.All:
                    PlayNext();
                    break;

                case RepeatMode.Off:
                    if (_isShuffle)
                    {
                        PlayNext();
                    }
                    else
                    {
                        if (_selectedSongIndex < _songs.Count - 1)
                        {
                            PlayNext();
                        }
                    }
                    break;
            }
        }

        public void LoadSongs()
        {
            _songs = AudioController.LoadSongs(Plugin.Settings.MediaFolder);
            _mediaFolder = Plugin.Settings.MediaFolder;
            RefreshDisplaySongs();
            _sortDirty = true;
        }

        private void RefreshDisplaySongs()
        {
            if (_activePlaylistId.HasValue)
            {
                _displaySongs = _plugin.PlaylistManager.GetPlaylistSongs(_activePlaylistId.Value, _songs);
            }
            else
            {
                _displaySongs = _songs;
            }

            ApplySearchFilter();
            _sortDirty = true;
        }

        private void ApplySearchFilter()
        {
            if (string.IsNullOrWhiteSpace(_searchTitle) && 
                string.IsNullOrWhiteSpace(_searchArtist) && 
                string.IsNullOrWhiteSpace(_searchAlbum))
            {
                _filteredSongs = _displaySongs;
            }
            else
            {
                var titleQuery = _searchTitle.ToLowerInvariant();
                var artistQuery = _searchArtist.ToLowerInvariant();
                var albumQuery = _searchAlbum.ToLowerInvariant();

                _filteredSongs = _displaySongs.Where(s =>
                    (string.IsNullOrWhiteSpace(_searchTitle) || s.Title.ToLowerInvariant().Contains(titleQuery)) &&
                    (string.IsNullOrWhiteSpace(_searchArtist) || s.Artist.ToLowerInvariant().Contains(artistQuery)) &&
                    (string.IsNullOrWhiteSpace(_searchAlbum) || s.Album.ToLowerInvariant().Contains(albumQuery))
                ).ToList();
            }
        }

        public void SetActivePlaylist(Guid? playlistId)
        {
            _activePlaylistId = playlistId;
            RefreshDisplaySongs();
            _selectedSongIndex = -1;
        }

        public Guid? GetActivePlaylistId() => _activePlaylistId;

        public List<Song> GetAllSongs() => _songs;

        public Song? GetCurrentSong()
        {
            return _currentSong;
        }

        private void PlaySong(Song song)
        {
            _currentSong = song;
            _selectedSongIndex = _displaySongs.IndexOf(song);
            _plugin.AudioController.Play(song);
            _plugin.UpdateDtr();

            if (_isShuffle && _shuffleQueue.Count > 0)
            {
                _shufflePosition = _shuffleQueue.IndexOf(_selectedSongIndex);
                if (_shufflePosition == -1)
                {
                    GenerateShuffleQueue();
                }
            }
        }

        private void GenerateShuffleQueue()
        {
            if (_shuffleQueue.Count == _displaySongs.Count && _lastShuffleCount == _displaySongs.Count)
                return;

            _shuffleQueue.Clear();
            for (int i = 0; i < _displaySongs.Count; i++)
            {
                _shuffleQueue.Add(i);
            }

            for (int i = _shuffleQueue.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (_shuffleQueue[i], _shuffleQueue[j]) = (_shuffleQueue[j], _shuffleQueue[i]);
            }

            _shufflePosition = 0;
            _lastShuffleCount = _displaySongs.Count;
        }

        private void PlayPrevious()
        {
            if (_displaySongs.Count == 0) return;

            int nextIndex;

            if (_isShuffle)
            {
                if (_shuffleQueue.Count != _displaySongs.Count)
                {
                    GenerateShuffleQueue();
                }

                _shufflePosition--;
                if (_shufflePosition < 0) _shufflePosition = _shuffleQueue.Count - 1;
                nextIndex = _shuffleQueue[_shufflePosition];
            }
            else
            {
                nextIndex = _selectedSongIndex - 1;
                if (nextIndex < 0) nextIndex = _displaySongs.Count - 1;
            }

            PlaySong(_displaySongs[nextIndex]);
        }

        private void PlayNext()
        {
            if (_displaySongs.Count == 0) return;

            int nextIndex;

            if (_isShuffle)
            {
                if (_shuffleQueue.Count != _displaySongs.Count)
                {
                    GenerateShuffleQueue();
                }

                _shufflePosition++;
                if (_shufflePosition >= _shuffleQueue.Count) _shufflePosition = 0;
                nextIndex = _shuffleQueue[_shufflePosition];
            }
            else
            {
                nextIndex = _selectedSongIndex + 1;
                if (nextIndex >= _displaySongs.Count) nextIndex = 0;
            }

            PlaySong(_displaySongs[nextIndex]);
        }

        private void ToggleShuffle()
        {
            _isShuffle = !_isShuffle;

            if (_isShuffle)
            {
                GenerateShuffleQueue();
            }
        }

        private void ToggleRepeat()
        {
            _repeatMode = (RepeatMode)(((int)_repeatMode + 1) % 3);
        }

        public bool IsShuffle => _isShuffle;
        public int GetRepeatMode() => (int)_repeatMode;

        public void ToggleShufflePublic() => ToggleShuffle();
        public void ToggleRepeatPublic() => ToggleRepeat();
        public void PlayPreviousPublic() => PlayPrevious();
        public void PlayNextPublic() => PlayNext();

        public void PlaySelectedSong()
        {
            if (_selectedSongIndex >= 0 && _selectedSongIndex < _displaySongs.Count)
            {
                PlaySong(_displaySongs[_selectedSongIndex]);
            }
        }

        public void PlayFirstSong()
        {
            if (_displaySongs.Count > 0)
            {
                _selectedSongIndex = 0;
                PlaySong(_displaySongs[0]);
            }
        }

        private static string FormatTime(float seconds)
        {
            var time = TimeSpan.FromSeconds(seconds);
            return $"{(int)time.TotalMinutes}:{time.Seconds:D2}";
        }

        private void DrawPlaylistMenu()
        {
            using var menu = ImRaii.Menu("Playlists");
            if (!menu) return;

            if (ImGui.MenuItem("Manage Playlists"))
            {
                _plugin.PlaylistWindow.Toggle();
            }

            ImGui.Separator();

            if (ImGui.MenuItem("All Songs", "", !_activePlaylistId.HasValue))
            {
                SetActivePlaylist(null);
            }

            var playlists = _plugin.PlaylistManager.Playlists.ToList();
            if (playlists.Count > 0)
            {
                ImGui.Separator();
                foreach (var playlist in playlists)
                {
                    bool isActive = _activePlaylistId.HasValue && _activePlaylistId.Value == playlist.Id;
                    if (ImGui.MenuItem($"{playlist.Name} ({playlist.SongPaths.Count} songs)", "", isActive))
                    {
                        SetActivePlaylist(playlist.Id);
                    }
                }
            }
        }

        private void DrawColumnsMenu()
        {
            using var menu = ImRaii.Menu("Columns");
            if (!menu) return;

            ImGui.Text("Visibility:");
            ImGui.Separator();

            if (ImGui.Checkbox("Artist", ref Plugin.Settings.ShowArtistColumn))
            {
                Plugin.Settings.Save();
            }

            if (ImGui.Checkbox("Album", ref Plugin.Settings.ShowAlbumColumn))
            {
                Plugin.Settings.Save();
            }

            if (ImGui.Checkbox("Length", ref Plugin.Settings.ShowLengthColumn))
            {
                Plugin.Settings.Save();
            }

            ImGui.Spacing();
            ImGui.Separator();

            if (ImGui.MenuItem("Reset to Defaults"))
            {
                Plugin.Settings.ShowArtistColumn = true;
                Plugin.Settings.ShowAlbumColumn = true;
                Plugin.Settings.ShowLengthColumn = true;
                Plugin.Settings.TitleColumnWidth = 0;
                Plugin.Settings.ArtistColumnWidth = 100;
                Plugin.Settings.AlbumColumnWidth = 100;
                Plugin.Settings.LengthColumnWidth = 85;
                Plugin.Settings.Save();
                _tableResetCounter++;
                _skipNextColumnSave = true;
            }
        }

        private void DrawAddToPlaylistMenu(Song song, List<Playlist> availablePlaylists)
        {
            using var menu = ImRaii.Menu("Add to Playlist");
            if (!menu) return;

            if (availablePlaylists.Count == 0)
            {
                ImGui.TextDisabled("No playlists available");
                return;
            }

            foreach (var playlist in availablePlaylists)
            {
                bool alreadyInPlaylist = playlist.SongPaths.Contains(song.FilePath);
                if (ImGui.MenuItem(playlist.Name, string.Empty, false, !alreadyInPlaylist))
                {
                    _plugin.PlaylistManager.AddSongToPlaylist(playlist.Id, song.FilePath);
                    _plugin.SaveSettings();
                }
                if (alreadyInPlaylist && ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Already in playlist");
                }
            }
        }

        private void DrawAddAlbumToPlaylistMenu(Song song, List<Playlist> availablePlaylists)
        {
            using var menu = ImRaii.Menu("Add Album to Playlist");
            if (!menu) return;

            if (availablePlaylists.Count == 0)
            {
                ImGui.TextDisabled("No playlists available");
                return;
            }

            var albumSongs = _songs.Where(s => s.Album == song.Album).ToList();
            ImGui.TextDisabled($"Album: {song.Album} ({albumSongs.Count} songs)");
            ImGui.Separator();

            foreach (var playlist in availablePlaylists)
            {
                int songsAlreadyInPlaylist = albumSongs.Count(s => playlist.SongPaths.Contains(s.FilePath));
                bool allInPlaylist = songsAlreadyInPlaylist == albumSongs.Count;

                if (ImGui.MenuItem(playlist.Name, string.Empty, false, !allInPlaylist))
                {
                    foreach (var albumSong in albumSongs)
                    {
                        if (!playlist.SongPaths.Contains(albumSong.FilePath))
                        {
                            _plugin.PlaylistManager.AddSongToPlaylist(playlist.Id, albumSong.FilePath);
                        }
                    }
                    _plugin.SaveSettings();
                }

                if (ImGui.IsItemHovered())
                {
                    if (allInPlaylist)
                    {
                        ImGui.SetTooltip("All songs already in playlist");
                    }
                    else if (songsAlreadyInPlaylist > 0)
                    {
                        ImGui.SetTooltip($"{songsAlreadyInPlaylist}/{albumSongs.Count} songs already in playlist");
                    }
                }
            }
        }

        private void DrawSongContextMenu(Song song)
        {
            var availablePlaylists = _plugin.PlaylistManager.Playlists.ToList();

            DrawAddToPlaylistMenu(song, availablePlaylists);
            DrawAddAlbumToPlaylistMenu(song, availablePlaylists);

            if (_activePlaylistId.HasValue)
            {
                if (ImGui.MenuItem("Remove from Playlist"))
                {
                    _plugin.PlaylistManager.RemoveSongFromPlaylist(_activePlaylistId.Value, song.FilePath);
                    _plugin.SaveSettings();
                    RefreshDisplaySongs();
                }
            }
        }

        public override void Draw()
        {
            using (var menuBar = ImRaii.MenuBar())
            {
                if (menuBar)
                {
                    if (ImGui.MenuItem("Settings"))
                    {
                        _plugin.SettingsWindow.Toggle();
                    }

                    if (ImGui.MenuItem("Mini Player"))
                    {
                        _plugin.MiniPlayerWindow.Toggle();
                    }

                    DrawPlaylistMenu();
                    DrawColumnsMenu();
                }
            }

            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(5, 5));
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(3, 0));

            float buttonWidth = 40f;
            float spacing = 3f;
            float totalWidth = (5 * buttonWidth) + (4 * spacing);
            float windowWidth = ImGui.GetContentRegionAvail().X;
            float offset = (windowWidth - totalWidth) / 2f;

            if (offset > 0)
            {
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset);
            }

            Vector4? shuffleColor = _isShuffle ? new Vector4(0, 1, 0.5f, 1) : null;
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Random, shuffleColor, activeColor: null, hoveredColor: null, size: new Vector2(40, 0)))
            {
                ToggleShuffle();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(_isShuffle ? "Shuffle: ON" : "Shuffle: OFF");
            }

            ImGui.SameLine();
            if (ImGuiComponents.IconButton(FontAwesomeIcon.StepBackward, new Vector2(40, 0)))
            {
                PlayPrevious();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Previous");
            }

            ImGui.SameLine();
            if (ImGuiComponents.IconButton(_plugin.AudioController.IsPlaying ? FontAwesomeIcon.Pause : FontAwesomeIcon.Play, new Vector2(40, 0)))
            {
                if (_plugin.AudioController.IsPlaying)
                {
                    _plugin.AudioController.Pause();
                    _plugin.UpdateDtr();
                }
                else if (_plugin.AudioController.IsPaused)
                {
                    _plugin.AudioController.Resume();
                    _plugin.UpdateDtr();
                }
                else if (_selectedSongIndex >= 0 && _selectedSongIndex < _displaySongs.Count)
                {
                    PlaySong(_displaySongs[_selectedSongIndex]);
                }
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(_plugin.AudioController.IsPlaying ? "Pause" : "Play");
            }

            ImGui.SameLine();
            if (ImGuiComponents.IconButton(FontAwesomeIcon.StepForward, new Vector2(40, 0)))
            {
                PlayNext();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Next");
            }

            ImGui.SameLine();
            Vector4? repeatColor = _repeatMode != RepeatMode.Off ? new Vector4(0, 1, 0.5f, 1) : null;
            var repeatIcon = _repeatMode switch
            {
                RepeatMode.Off => FontAwesomeIcon.Redo,
                RepeatMode.All => FontAwesomeIcon.Redo,
                RepeatMode.One => FontAwesomeIcon.Music,
                _ => FontAwesomeIcon.Redo
            };
            if (ImGuiComponents.IconButton(repeatIcon, repeatColor, activeColor: null, hoveredColor: null, size: new Vector2(40, 0)))
            {
                ToggleRepeat();
            }
            if (ImGui.IsItemHovered())
            {
                string repeatTooltip = _repeatMode switch
                {
                    RepeatMode.Off => "Repeat: OFF",
                    RepeatMode.All => "Repeat: ALL",
                    RepeatMode.One => "Repeat: ONE",
                    _ => "Repeat: OFF"
                };
                ImGui.SetTooltip(repeatTooltip);
            }

            ImGui.PopStyleVar(2);

            ImGui.Spacing();

            float currentSeconds = (float)_plugin.AudioController.CurrentTime.TotalSeconds;
            float totalSeconds = (float)_plugin.AudioController.TotalTime.TotalSeconds;

            bool hasAudio = _plugin.AudioController.HasAudio;

            using (ImRaii.Disabled(!hasAudio))
            {
                using var itemWidth = ImRaii.ItemWidth(-1);

                if (ImGui.SliderFloat("##Progress", ref currentSeconds, 0, Math.Max(totalSeconds, 1), FormatTime(currentSeconds)))
                {
                    if (hasAudio)
                    {
                        _plugin.AudioController.SetPosition(TimeSpan.FromSeconds(currentSeconds));
                    }
                }
            }

            ImGui.Spacing();

            if (_currentSong != null)
            {
                ImGui.TextColored(new Vector4(0, 1, 0, 1), $"Now Playing: {_currentSong.Title} - {_currentSong.Artist}");
            }
            else if (_selectedSongIndex >= 0 && _selectedSongIndex < _displaySongs.Count)
            {
                ImGui.TextColored(new Vector4(1, 1, 0, 1), $"Selected: {_displaySongs[_selectedSongIndex].Title} - {_displaySongs[_selectedSongIndex].Artist}");
            }
            else
            {
                ImGui.TextDisabled("No music selected.");
            }

            ImGui.Spacing();

            int columnCount = 1;
            if (Plugin.Settings.ShowArtistColumn) columnCount++;
            if (Plugin.Settings.ShowAlbumColumn) columnCount++;
            if (Plugin.Settings.ShowLengthColumn) columnCount++;

            using (var table = ImRaii.Table($"SongTable##{_tableResetCounter}", columnCount, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.Resizable | ImGuiTableFlags.Sortable))
            {
                if (table)
                {
                ImGui.TableSetupColumn("Title", ImGuiTableColumnFlags.WidthStretch | ImGuiTableColumnFlags.NoHide | ImGuiTableColumnFlags.DefaultSort | ImGuiTableColumnFlags.PreferSortAscending);

                if (Plugin.Settings.ShowArtistColumn)
                {
                    ImGui.TableSetupColumn("Artist", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.PreferSortAscending, Math.Max(Plugin.Settings.ArtistColumnWidth, 80));
                }
                if (Plugin.Settings.ShowAlbumColumn)
                {
                    ImGui.TableSetupColumn("Album", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.PreferSortAscending, Math.Max(Plugin.Settings.AlbumColumnWidth, 80));
                }
                if (Plugin.Settings.ShowLengthColumn)
                {
                    ImGui.TableSetupColumn("Length", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.PreferSortAscending, Math.Max(Plugin.Settings.LengthColumnWidth, 50));
                }

                ImGui.TableSetupScrollFreeze(0, 2);
                ImGui.TableHeadersRow();

                ImGui.TableNextRow(ImGuiTableRowFlags.Headers);

                ImGui.TableSetColumnIndex(0);
                ImGui.SetNextItemWidth(-1);
                ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(0, 0));
                if (ImGui.InputTextWithHint("##SearchTitle", "", ref _searchTitle, 256))
                {
                    ApplySearchFilter();
                }
                ImGui.PopStyleVar();

                int searchCol = 1;
                if (Plugin.Settings.ShowArtistColumn)
                {
                    ImGui.TableSetColumnIndex(searchCol);
                    ImGui.SetNextItemWidth(-1);
                    ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(0, 0));
                    if (ImGui.InputTextWithHint("##SearchArtist", "", ref _searchArtist, 256))
                    {
                        ApplySearchFilter();
                    }
                    ImGui.PopStyleVar();
                    searchCol++;
                }
                if (Plugin.Settings.ShowAlbumColumn)
                {
                    ImGui.TableSetColumnIndex(searchCol);
                    ImGui.SetNextItemWidth(-1);
                    ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(0, 0));
                    if (ImGui.InputTextWithHint("##SearchAlbum", "", ref _searchAlbum, 256))
                    {
                        ApplySearchFilter();
                    }
                    ImGui.PopStyleVar();
                    searchCol++;
                }
                if (Plugin.Settings.ShowLengthColumn)
                {
                    ImGui.TableSetColumnIndex(searchCol);
                    ImGui.Text("");
                }


                var sortSpecs = ImGui.TableGetSortSpecs();
                if (sortSpecs.SpecsDirty || _sortDirty)
                {
                    if (_displaySongs.Count > 1)
                    {
                        if (_displaySongs.Count > 0 && sortSpecs.Specs.ColumnIndex >= 0)
                        {
                            var spec = sortSpecs.Specs;
                            bool ascending = spec.SortDirection == ImGuiSortDirection.Ascending;

                            int actualColumnIndex = spec.ColumnIndex;

                            if (actualColumnIndex == 0)
                            {
                                _songs = ascending 
                                    ? _songs.OrderBy(s => s.Title).ToList()
                                    : _songs.OrderByDescending(s => s.Title).ToList();
                                RefreshDisplaySongs();
                            }
                            else
                            {
                                int visibleCol = 1;
                                if (Plugin.Settings.ShowArtistColumn)
                                {
                                    if (actualColumnIndex == visibleCol)
                                    {
                                        _displaySongs = ascending
                                            ? _displaySongs.OrderBy(s => s.Artist).ThenBy(s => s.Title).ToList()
                                            : _displaySongs.OrderByDescending(s => s.Artist).ThenByDescending(s => s.Title).ToList();
                                        ApplySearchFilter();
                                    }
                                    visibleCol++;
                                }
                                if (Plugin.Settings.ShowAlbumColumn)
                                {
                                    if (actualColumnIndex == visibleCol)
                                    {
                                        _displaySongs = ascending
                                            ? _displaySongs.OrderBy(s => s.Album).ThenBy(s => s.Title).ToList()
                                            : _displaySongs.OrderByDescending(s => s.Album).ThenByDescending(s => s.Title).ToList();
                                        ApplySearchFilter();
                                    }
                                    visibleCol++;
                                }
                                if (Plugin.Settings.ShowLengthColumn)
                                {
                                    if (actualColumnIndex == visibleCol)
                                    {
                                        _displaySongs = ascending
                                            ? _displaySongs.OrderBy(s => s.Duration).ThenBy(s => s.Title).ToList()
                                            : _displaySongs.OrderByDescending(s => s.Duration).ThenByDescending(s => s.Title).ToList();
                                        ApplySearchFilter();
                                    }
                                }
                            }

                            _lastSortSpecs = spec;
                            sortSpecs.SpecsDirty = false;
                            _sortDirty = false;
                        }
                    }
                }

                for (int i = 0; i < _filteredSongs.Count; i++)
                {
                    var song = _filteredSongs[i];
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    bool isSelected = _selectedSongIndex >= 0 && _selectedSongIndex < _displaySongs.Count && _displaySongs[_selectedSongIndex] == song;
                    if (ImGui.Selectable(song.Title, isSelected, ImGuiSelectableFlags.SpanAllColumns))
                    {
                        _selectedSongIndex = _displaySongs.IndexOf(song);
                        PlaySong(song);
                    }

                    using (var popup = ImRaii.ContextPopupItem($"songContext_{i}"))
                    {
                        if (popup)
                        {
                            DrawSongContextMenu(song);
                        }
                    }

                        if (Plugin.Settings.ShowArtistColumn)
                        {
                            ImGui.TableNextColumn();
                            ImGui.Text(song.Artist);
                        }

                        if (Plugin.Settings.ShowAlbumColumn)
                        {
                            ImGui.TableNextColumn();
                            ImGui.Text(song.Album);
                        }

                        if (Plugin.Settings.ShowLengthColumn)
                        {
                            ImGui.TableNextColumn();
                            ImGui.Text(song.FormattedDuration);
                        }
                    }

                    if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                    {
                        if (_skipNextColumnSave)
                        {
                            _skipNextColumnSave = false;
                        }
                        else
                        {
                            bool needsSave = false;

                            if (Plugin.Settings.ShowArtistColumn)
                            {
                                float newArtistWidth = ImGui.GetColumnWidth(1);
                                if (Math.Abs(newArtistWidth - Plugin.Settings.ArtistColumnWidth) > 1.0f)
                                {
                                    Plugin.Settings.ArtistColumnWidth = newArtistWidth;
                                    needsSave = true;
                                }
                            }

                            int colIndex = Plugin.Settings.ShowArtistColumn ? 2 : 1;
                            if (Plugin.Settings.ShowAlbumColumn)
                            {
                                float newAlbumWidth = ImGui.GetColumnWidth(colIndex);
                                if (Math.Abs(newAlbumWidth - Plugin.Settings.AlbumColumnWidth) > 1.0f)
                                {
                                    Plugin.Settings.AlbumColumnWidth = newAlbumWidth;
                                    needsSave = true;
                                }
                                colIndex++;
                            }

                            if (Plugin.Settings.ShowLengthColumn)
                            {
                                float newLengthWidth = ImGui.GetColumnWidth(colIndex);
                                if (Math.Abs(newLengthWidth - Plugin.Settings.LengthColumnWidth) > 1.0f)
                                {
                                    Plugin.Settings.LengthColumnWidth = newLengthWidth;
                                    needsSave = true;
                                }
                            }

                            if (needsSave)
                            {
                                Plugin.Settings.Save();
                            }
                        }
                    }
                }
            }
        }
    }
}