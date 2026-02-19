using System;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace AscianMusicPlayer.Windows
{
    public class MiniPlayerWindow : Window
    {
        private readonly Plugin _plugin;
        private float _scrollOffset = 0f;
        private DateTime _lastScrollUpdate = DateTime.UtcNow;
        private string _lastSongText = string.Empty;

        public MiniPlayerWindow(Plugin plugin) : base("Ascian Mini Player###AscianMiniPlayer")
        {
            _plugin = plugin;
            this.Size = new Vector2(400, 125);
            this.SizeCondition = ImGuiCond.Always;
            this.Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoResize;
        }

        public override void Draw()
        {
            var currentSong = _plugin.MainWindow.GetCurrentSong();

            if (currentSong != null)
            {
                string songText = $"{currentSong.Title} - {currentSong.Artist}";
                float windowWidth = ImGui.GetContentRegionAvail().X;
                float textWidth = ImGui.CalcTextSize(songText).X;

                if (songText != _lastSongText)
                {
                    _scrollOffset = 0f;
                    _lastSongText = songText;
                }

                if (textWidth > windowWidth)
                {
                    var now = DateTime.UtcNow;
                    float deltaTime = (float)(now - _lastScrollUpdate).TotalSeconds;
                    _lastScrollUpdate = now;

                    _scrollOffset += 30f * deltaTime;

                    float scrollLimit = textWidth + 50f;
                    if (_scrollOffset > scrollLimit)
                    {
                        _scrollOffset = 0f;
                    }

                    ImGui.BeginChild("##ScrollingText", new Vector2(windowWidth, ImGui.GetTextLineHeight()), false, ImGuiWindowFlags.NoScrollbar);

                    ImGui.SetCursorPosX(-_scrollOffset);
                    ImGui.TextColored(new Vector4(0, 1, 0, 1), songText);

                    ImGui.SameLine(0, 50f);
                    ImGui.TextColored(new Vector4(0, 1, 0, 1), songText);

                    ImGui.EndChild();
                }
                else
                {
                    float offset = (windowWidth - textWidth) / 2f;
                    if (offset > 0)
                    {
                        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset);
                    }
                    ImGui.TextColored(new Vector4(0, 1, 0, 1), songText);
                }
            }
            else
            {
                string noSongText = "No song playing";
                float windowWidth = ImGui.GetContentRegionAvail().X;
                float textWidth = ImGui.CalcTextSize(noSongText).X;
                float offset = (windowWidth - textWidth) / 2f;
                if (offset > 0)
                {
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset);
                }
                ImGui.TextDisabled(noSongText);
                _lastSongText = string.Empty;
            }

            ImGui.Spacing();

            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(5, 5));
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(3, 0));

            float buttonWidth = 40f;
            float spacing = 3f;
            float totalWidth = (5 * buttonWidth) + (4 * spacing);
            float windowWidth2 = ImGui.GetContentRegionAvail().X;
            float offset2 = (windowWidth2 - totalWidth) / 2f;

            if (offset2 > 0)
            {
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset2);
            }

            bool isShuffle = _plugin.MainWindow.IsShuffle;
            Vector4? shuffleColor = isShuffle ? new Vector4(0, 1, 0.5f, 1) : null;
            if (ImGuiComponents.IconButton("##MiniShuffle", FontAwesomeIcon.Random, shuffleColor, activeColor: null, hoveredColor: null, size: new Vector2(40, 0)))
            {
                _plugin.MainWindow.ToggleShufflePublic();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(isShuffle ? "Shuffle: ON" : "Shuffle: OFF");
            }

            ImGui.SameLine();
            if (ImGuiComponents.IconButton("##MiniPrevious", FontAwesomeIcon.StepBackward, new Vector2(40, 0)))
            {
                _plugin.MainWindow.PlayPreviousPublic();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Previous");
            }

            ImGui.SameLine();
            if (ImGuiComponents.IconButton("##MiniPlayPause", _plugin.AudioController.IsPlaying ? FontAwesomeIcon.Pause : FontAwesomeIcon.Play, new Vector2(40, 0)))
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
                else
                {
                    _plugin.MainWindow.PlaySelectedSong();
                }
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(_plugin.AudioController.IsPlaying ? "Pause" : "Play");
            }

            ImGui.SameLine();
            if (ImGuiComponents.IconButton("##MiniNext", FontAwesomeIcon.StepForward, new Vector2(40, 0)))
            {
                _plugin.MainWindow.PlayNextPublic();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Next");
            }

            ImGui.SameLine();
            var repeatMode = _plugin.MainWindow.GetRepeatMode();
            Vector4? repeatColor = repeatMode != 0 ? new Vector4(0, 1, 0.5f, 1) : null;
            var repeatIcon = repeatMode switch
            {
                0 => FontAwesomeIcon.Redo,
                1 => FontAwesomeIcon.Redo,
                2 => FontAwesomeIcon.Music,
                _ => FontAwesomeIcon.Redo
            };
            if (ImGuiComponents.IconButton("##MiniRepeat", repeatIcon, repeatColor, activeColor: null, hoveredColor: null, size: new Vector2(40, 0)))
            {
                _plugin.MainWindow.ToggleRepeatPublic();
            }
            if (ImGui.IsItemHovered())
            {
                string repeatTooltip = repeatMode switch
                {
                    0 => "Repeat: OFF",
                    1 => "Repeat: ALL",
                    2 => "Repeat: ONE",
                    _ => "Repeat: OFF"
                };
                ImGui.SetTooltip(repeatTooltip);
            }

            ImGui.PopStyleVar(2);

            ImGui.Spacing();

            float currentSeconds = (float)_plugin.AudioController.CurrentTime.TotalSeconds;
            float totalSeconds = (float)_plugin.AudioController.TotalTime.TotalSeconds;
            bool hasAudio = _plugin.AudioController.HasAudio;

            if (!hasAudio)
            {
                ImGui.BeginDisabled();
            }

            ImGui.PushItemWidth(-1);
            if (ImGui.SliderFloat("##MiniProgress", ref currentSeconds, 0, Math.Max(totalSeconds, 1), FormatTime(currentSeconds)))
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
        }

        private static string FormatTime(float seconds)
        {
            var time = TimeSpan.FromSeconds(seconds);
            return $"{(int)time.TotalMinutes}:{time.Seconds:D2}";
        }
    }
}
