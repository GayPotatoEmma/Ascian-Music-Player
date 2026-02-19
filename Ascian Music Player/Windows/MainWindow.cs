using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AscianMusicPlayer.Audio;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

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

            _plugin.AudioController.SongEnded += OnSongEnded;
        }

        public override void OnClose()
        {
            _plugin.AudioController.SongEnded -= OnSongEnded;
            base.OnClose();
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
            _songs = _plugin.AudioController.LoadSongs(Plugin.Settings.MediaFolder);
            _mediaFolder = Plugin.Settings.MediaFolder;
            _sortDirty = true;
        }

        public Song? GetCurrentSong()
        {
            return _currentSong;
        }

        private void PlaySong(Song song)
        {
            _currentSong = song;
            _selectedSongIndex = _songs.IndexOf(song);
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
            if (_shuffleQueue.Count == _songs.Count && _lastShuffleCount == _songs.Count)
                return;

            _shuffleQueue.Clear();
            for (int i = 0; i < _songs.Count; i++)
            {
                _shuffleQueue.Add(i);
            }

            for (int i = _shuffleQueue.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (_shuffleQueue[i], _shuffleQueue[j]) = (_shuffleQueue[j], _shuffleQueue[i]);
            }

            _shufflePosition = 0;
            _lastShuffleCount = _songs.Count;
        }

        private void PlayPrevious()
        {
            if (_songs.Count == 0) return;

            int nextIndex;

            if (_isShuffle)
            {
                if (_shuffleQueue.Count != _songs.Count)
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
                if (nextIndex < 0) nextIndex = _songs.Count - 1;
            }

            PlaySong(_songs[nextIndex]);
        }

        private void PlayNext()
        {
            if (_songs.Count == 0) return;

            int nextIndex;

            if (_isShuffle)
            {
                if (_shuffleQueue.Count != _songs.Count)
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
                if (nextIndex >= _songs.Count) nextIndex = 0;
            }

            PlaySong(_songs[nextIndex]);
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
            if (_selectedSongIndex >= 0 && _selectedSongIndex < _songs.Count)
            {
                PlaySong(_songs[_selectedSongIndex]);
            }
        }

        private static string FormatTime(float seconds)
        {
            var time = TimeSpan.FromSeconds(seconds);
            return $"{(int)time.TotalMinutes}:{time.Seconds:D2}";
        }

        public override void Draw()
        {
            if (ImGui.BeginMenuBar())
            {
                if (ImGui.MenuItem("Settings"))
                {
                    _plugin.SettingsWindow.Toggle();
                }

                if (ImGui.MenuItem("Mini Player"))
                {
                    _plugin.MiniPlayerWindow.Toggle();
                }

                if (ImGui.BeginMenu("Columns"))
                {
                    ImGui.Text("Visibility:");
                    ImGui.Separator();

                    bool showArtist = Plugin.Settings.ShowArtistColumn;
                    bool showAlbum = Plugin.Settings.ShowAlbumColumn;
                    bool showLength = Plugin.Settings.ShowLengthColumn;

                    if (ImGui.Checkbox("Artist", ref showArtist))
                    {
                        Plugin.Settings.ShowArtistColumn = showArtist;
                        Plugin.Settings.Save();
                    }

                    if (ImGui.Checkbox("Album", ref showAlbum))
                    {
                        Plugin.Settings.ShowAlbumColumn = showAlbum;
                        Plugin.Settings.Save();
                    }

                    if (ImGui.Checkbox("Length", ref showLength))
                    {
                        Plugin.Settings.ShowLengthColumn = showLength;
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

                    ImGui.EndMenu();
                }

                ImGui.EndMenuBar();
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

            // Shuffle button with conditional color
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
            // Previous button
            if (ImGuiComponents.IconButton(FontAwesomeIcon.StepBackward, new Vector2(40, 0)))
            {
                PlayPrevious();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Previous");
            }

            ImGui.SameLine();
            // Play/Pause button
            if (ImGuiComponents.IconButton(_plugin.AudioController.IsPlaying ? FontAwesomeIcon.Pause : FontAwesomeIcon.Play, new Vector2(40, 0)))
            {
                if (_plugin.AudioController.IsPlaying)
                {
                    _plugin.AudioController.Pause();
                    _plugin.UpdateDtr(); // Update DTR when paused
                }
                else if (_plugin.AudioController.IsPaused)
                {
                    _plugin.AudioController.Resume();
                    _plugin.UpdateDtr(); // Update DTR when resumed
                }
                else if (_selectedSongIndex >= 0 && _selectedSongIndex < _songs.Count)
                {
                    PlaySong(_songs[_selectedSongIndex]);
                }
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(_plugin.AudioController.IsPlaying ? "Pause" : "Play");
            }

            ImGui.SameLine();
            // Next button
            if (ImGuiComponents.IconButton(FontAwesomeIcon.StepForward, new Vector2(40, 0)))
            {
                PlayNext();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Next");
            }

            ImGui.SameLine();
            // Repeat button with conditional color
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

            // Music Progress Bar - always visible
            float currentSeconds = (float)_plugin.AudioController.CurrentTime.TotalSeconds;
            float totalSeconds = (float)_plugin.AudioController.TotalTime.TotalSeconds;

            bool hasAudio = _plugin.AudioController.HasAudio;

            if (!hasAudio)
            {
                ImGui.BeginDisabled();
            }

            ImGui.PushItemWidth(-1);
            if (ImGui.SliderFloat("##Progress", ref currentSeconds, 0, Math.Max(totalSeconds, 1), FormatTime(currentSeconds)))
            {
                if (hasAudio)
                {
                    _plugin.AudioController.SetPosition(TimeSpan.FromSeconds(currentSeconds));
                }
            }
            ImGui.PopItemWidth();

            if (!hasAudio)
            {
                ImGui.EndDisabled();
            }

            ImGui.Spacing();

            if (_currentSong != null)
            {
                ImGui.TextColored(new Vector4(0, 1, 0, 1), $"Now Playing: {_currentSong.Title} - {_currentSong.Artist}");
            }
            else if (_selectedSongIndex >= 0 && _selectedSongIndex < _songs.Count)
            {
                ImGui.TextColored(new Vector4(1, 1, 0, 1), $"Selected: {_songs[_selectedSongIndex].Title} - {_songs[_selectedSongIndex].Artist}");
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

            if (ImGui.BeginTable($"SongTable##{_tableResetCounter}", columnCount, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.Resizable | ImGuiTableFlags.Sortable))
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

                ImGui.TableHeadersRow();

                var sortSpecs = ImGui.TableGetSortSpecs();
                if (sortSpecs.SpecsDirty || _sortDirty)
                {
                    if (_songs.Count > 1)
                    {
                        if (_songs.Count > 0 && sortSpecs.Specs.ColumnIndex >= 0)
                        {
                            var spec = sortSpecs.Specs;
                            bool ascending = spec.SortDirection == ImGuiSortDirection.Ascending;

                            int actualColumnIndex = spec.ColumnIndex;

                            if (actualColumnIndex == 0)
                            {
                                _songs = ascending 
                                    ? _songs.OrderBy(s => s.Title).ToList()
                                    : _songs.OrderByDescending(s => s.Title).ToList();
                            }
                            else
                            {
                                int visibleCol = 1;
                                if (Plugin.Settings.ShowArtistColumn)
                                {
                                    if (actualColumnIndex == visibleCol)
                                    {
                                        _songs = ascending
                                            ? _songs.OrderBy(s => s.Artist).ThenBy(s => s.Title).ToList()
                                            : _songs.OrderByDescending(s => s.Artist).ThenByDescending(s => s.Title).ToList();
                                    }
                                    visibleCol++;
                                }
                                if (Plugin.Settings.ShowAlbumColumn)
                                {
                                    if (actualColumnIndex == visibleCol)
                                    {
                                        _songs = ascending
                                            ? _songs.OrderBy(s => s.Album).ThenBy(s => s.Title).ToList()
                                            : _songs.OrderByDescending(s => s.Album).ThenByDescending(s => s.Title).ToList();
                                    }
                                    visibleCol++;
                                }
                                if (Plugin.Settings.ShowLengthColumn)
                                {
                                    if (actualColumnIndex == visibleCol)
                                    {
                                        _songs = ascending
                                            ? _songs.OrderBy(s => s.Duration).ThenBy(s => s.Title).ToList()
                                            : _songs.OrderByDescending(s => s.Duration).ThenByDescending(s => s.Title).ToList();
                                    }
                                }
                            }

                            _lastSortSpecs = spec;
                            sortSpecs.SpecsDirty = false;
                            _sortDirty = false;
                        }
                    }
                }

                for (int i = 0; i < _songs.Count; i++)
                {
                    var song = _songs[i];
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    if (ImGui.Selectable(song.Title, i == _selectedSongIndex, ImGuiSelectableFlags.SpanAllColumns))
                    {
                        _selectedSongIndex = i;
                        PlaySong(song);
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

                ImGui.EndTable();
            }
        }
    }
}