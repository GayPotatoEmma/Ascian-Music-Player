using System;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace AscianMusicPlayer.Windows
{
    public class PlaylistWindow : Window
    {
        private readonly Plugin _plugin;
        private Guid? _selectedPlaylistId;
        private string _newPlaylistName = string.Empty;
        private string _renamePlaylistName = string.Empty;
        private Guid? _renamingPlaylistId;

        public PlaylistWindow(Plugin plugin) : base("Playlists###AscianMusicPlayerPlaylists")
        {
            _plugin = plugin;
            this.Size = new Vector2(450, 500);
            this.SizeCondition = ImGuiCond.FirstUseEver;
            this.SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(400, 350),
                MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
            };
        }

        public override void Draw()
        {
            ImGui.Text("Your Playlists");
            ImGui.Separator();
            ImGui.Spacing();

            var scale = ImGui.GetIO().FontGlobalScale;
            float buttonWidth = 120 * scale;
            float spacing = ImGui.GetStyle().ItemSpacing.X;
            float availableWidth = ImGui.GetContentRegionAvail().X;
            float inputWidth = availableWidth - buttonWidth - spacing;

            ImGui.SetNextItemWidth(inputWidth);
            ImGui.InputTextWithHint("##NewPlaylistName", "Enter playlist name...", ref _newPlaylistName, 100);
            ImGui.SameLine();
            if (ImGui.Button("Create Playlist", new Vector2(buttonWidth, 0)))
            {
                if (!string.IsNullOrWhiteSpace(_newPlaylistName))
                {
                    _plugin.PlaylistManager.CreatePlaylist(_newPlaylistName.Trim());
                    _newPlaylistName = string.Empty;
                }
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            using (var child = ImRaii.Child("PlaylistList", new Vector2(0, 0), true))
            {
                var playlists = _plugin.PlaylistManager.Playlists;

                if (playlists.Count == 0)
                {
                    ImGui.TextDisabled("No playlists yet. Create one to get started!");
                }
                else
                {
                    using (var table = ImRaii.Table("PlaylistsTable", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
                    {
                        if (table)
                        {
                            var tableScale = ImGui.GetIO().FontGlobalScale;
                            ImGui.TableSetupColumn("Playlist", ImGuiTableColumnFlags.WidthStretch);
                            ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, 100 * tableScale);

                            Guid? playlistToDelete = null;

                            foreach (var playlist in playlists)
                            {
                                using (var id = ImRaii.PushId(playlist.Id.ToString()))
                                {
                                    ImGui.TableNextRow();

                                    ImGui.TableNextColumn();

                                    if (_renamingPlaylistId == playlist.Id)
                                    {
                                        ImGui.SetNextItemWidth(-1);
                                        if (ImGui.InputText("##rename", ref _renamePlaylistName, 100, ImGuiInputTextFlags.EnterReturnsTrue))
                                        {
                                            if (!string.IsNullOrWhiteSpace(_renamePlaylistName))
                                            {
                                                _plugin.PlaylistManager.RenamePlaylist(playlist.Id, _renamePlaylistName.Trim());
                                            }
                                            _renamingPlaylistId = null;
                                            _renamePlaylistName = string.Empty;
                                        }

                                        if (ImGui.IsItemDeactivated() && !ImGui.IsItemDeactivatedAfterEdit())
                                        {
                                            _renamingPlaylistId = null;
                                            _renamePlaylistName = string.Empty;
                                        }
                                    }
                                    else
                                    {
                                        ImGui.AlignTextToFramePadding();
                                        ImGui.Text($"{playlist.Name} ({playlist.SongCount} songs)");
                                    }

                                    ImGui.TableNextColumn();

                                    if (ImGuiComponents.IconButton("play", FontAwesomeIcon.Play))
                                    {
                                        var playlistSongs = _plugin.PlaylistManager.GetPlaylistSongs(playlist.Id, _plugin.MainWindow.GetAllSongs());
                                        if (playlistSongs.Count > 0)
                                        {
                                            _plugin.MainWindow.SetActivePlaylist(playlist.Id);
                                            _plugin.MainWindow.IsOpen = true;
                                            _plugin.MainWindow.PlayFirstSong();
                                        }
                                    }
                                    if (ImGui.IsItemHovered())
                                    {
                                        ImGui.SetTooltip("Play Playlist");
                                    }

                                    ImGui.SameLine();

                                    if (ImGuiComponents.IconButton("edit", FontAwesomeIcon.Edit))
                                    {
                                        _renamingPlaylistId = playlist.Id;
                                        _renamePlaylistName = playlist.Name;
                                    }
                                    if (ImGui.IsItemHovered())
                                    {
                                        ImGui.SetTooltip("Rename");
                                    }

                                    ImGui.SameLine();

                                    bool shiftHeld = ImGui.GetIO().KeyShift;

                                    using (var disabled = ImRaii.Disabled(!shiftHeld))
                                    {
                                        if (ImGuiComponents.IconButton("delete", FontAwesomeIcon.Trash))
                                        {
                                            playlistToDelete = playlist.Id;
                                        }
                                    }

                                    if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                                    {
                                        if (shiftHeld)
                                        {
                                            ImGui.SetTooltip("Delete");
                                        }
                                        else
                                        {
                                            ImGui.SetTooltip("Hold Shift and click to delete!");
                                        }
                                    }
                                }
                            }

                            if (playlistToDelete.HasValue)
                            {
                                _plugin.PlaylistManager.DeletePlaylist(playlistToDelete.Value);
                                if (_selectedPlaylistId == playlistToDelete.Value)
                                {
                                    _selectedPlaylistId = null;
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
