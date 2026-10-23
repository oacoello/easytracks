using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using EasyTracks.Domain.Models;

namespace EasyTracks_UI;

public sealed partial class MainPage : Page
{
    public ViewModels.MainViewModel ViewModel { get; } = new();

    public MainPage()
    {
        App.MainViewModelInstance = ViewModel;
        InitializeComponent();

        this.Loaded += (_, _) =>
        {
            (App.MainWindowInstance as MainWindow)?.ApplyTitleBarTheme(this.ActualTheme);
            if (TopTransportBar != null)
            {
                TopTransportBar.SetActiveTab(ViewModel.CurrentTab);
            }
        };

        // Wire position change to auto-scroll waveform ribbon seamlessly
        ViewModel.Transport.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ViewModels.TransportViewModel.CurrentPositionSeconds))
            {
                var dq = this.DispatcherQueue;
                if (dq != null && !dq.HasThreadAccess)
                    dq.TryEnqueue(UpdateWaveformScroll);
                else
                    UpdateWaveformScroll();
            }
        };
    }

    private void OnMasterSliderDoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        ViewModel.Transport.MasterVolume = 1.0;
        e.Handled = true;
    }

    private void OnPadVolumeSliderDoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        ViewModel.Transport.PadVolume = 0.75;
        e.Handled = true;
    }

    private void OnResetSectionsToSingleSpanClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.ResetToSingleUnsectionedWaveform();
    }

    private void OnWaveformScrollViewerSizeChanged(object sender, SizeChangedEventArgs e)
    {
        double w = WaveformScrollViewer.ViewportWidth > 0 ? WaveformScrollViewer.ViewportWidth : e.NewSize.Width;
        if (w > 100)
        {
            ViewModel.SetWaveformViewportWidth(w);
            UpdateWaveformScroll();
        }
    }

    private void OnSetlistDragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
    {
        ViewModel.SaveData();
    }

    private void OnApplySectionRenameClicked(object sender, RoutedEventArgs e)
    {
        if (SectionRenameTextBox != null && !string.IsNullOrWhiteSpace(SectionRenameTextBox.Text))
        {
            ViewModel.RenameSection(ViewModel.SelectedSectionForEdit, SectionRenameTextBox.Text);
        }
    }

    private void OnInstrumentBusOutputClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement btn || btn.DataContext is not ViewModels.InstrumentBusViewModel busVm) return;

        var flyout = new MenuFlyout();

        void AddOption(MenuFlyoutSubItem? parent, string label, int busIdx)
        {
            var item = new ToggleMenuFlyoutItem
            {
                Text = label,
                IsChecked = busVm.OutputBusIndex == busIdx
            };
            item.Click += (_, _) => busVm.OutputBusIndex = busIdx;
            if (parent != null) parent.Items.Add(item);
            else flyout.Items.Add(item);
        }

        AddOption(null, "🔊 Out 1-2 (Estéreo Master)", 0);
        AddOption(null, "🎧 In-Ears L (Split)", -1);
        AddOption(null, "🎚️ FOH R (Split)", -2);
        flyout.Items.Add(new MenuFlyoutSeparator());

        var pairsMenu = new MenuFlyoutSubItem { Text = "🎛️ Mackie DL16S • Pares Estéreo" };
        for (int ch = 1; ch <= 15; ch += 2)
        {
            AddOption(pairsMenu, $"USB Out {ch}-{ch + 1}", 100 + ch);
        }
        flyout.Items.Add(pairsMenu);

        var monoMenu = new MenuFlyoutSubItem { Text = "🎛️ Mackie DL16S • Canales USB 1 - 16" };
        for (int ch = 1; ch <= 16; ch++)
        {
            AddOption(monoMenu, $"USB Canal {ch}", ch);
        }
        flyout.Items.Add(monoMenu);

        flyout.ShowAt(btn);
    }

    private void UpdateWaveformScroll()
    {
        double totalSecs = ViewModel.Transport.TotalDurationSeconds;
        if (totalSecs <= 0) return;

        double curSecs = ViewModel.Transport.CurrentPositionSeconds;
        double playheadX = 0;
        int sectionCount = ViewModel.ActiveSections.Count;
        double totalTrackWidth = ViewModel.ActiveSections.Sum(s => s.SectionWidth);
        if (totalTrackWidth <= 0) totalTrackWidth = 800.0;

        if (sectionCount > 0)
        {
            bool matched = false;
            double accumulatedX = 0;
            for (int i = 0; i < sectionCount; i++)
            {
                var sec = ViewModel.ActiveSections[i];
                double start = sec.Model.StartTime.TotalSeconds;
                double end = sec.Model.EndTime.TotalSeconds;
                double secWidth = sec.SectionWidth > 0 ? sec.SectionWidth : 200.0;

                if (curSecs >= start && (curSecs <= end || i == sectionCount - 1))
                {
                    double localRatio = Math.Clamp((curSecs - start) / Math.Max(0.1, end - start), 0.0, 1.0);
                    playheadX = accumulatedX + (localRatio * secWidth);
                    matched = true;
                    break;
                }
                accumulatedX += secWidth;
            }

            if (!matched)
            {
                double progress = Math.Clamp(curSecs / totalSecs, 0.0, 1.0);
                playheadX = progress * totalTrackWidth;
            }
        }
        else
        {
            double progress = Math.Clamp(curSecs / totalSecs, 0.0, 1.0);
            playheadX = progress * totalTrackWidth;
        }

        if (PlayheadLine != null)
        {
            Canvas.SetLeft(PlayheadLine, playheadX);
        }
        // NOTE: Do NOT call WaveformScrollViewer.ChangeView here so user manual scrolling is never hijacked.
    }

    private void OnWaveformTrackPointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (ViewModel.Transport.TotalDurationSeconds <= 0) return;
        var pt = e.GetCurrentPoint(WaveformTrackGrid);
        double trackWidth = ViewModel.ActiveSections.Sum(s => s.SectionWidth);
        if (trackWidth <= 0) trackWidth = WaveformTrackGrid.ActualWidth;
        if (trackWidth > 0)
        {
            double ratio = Math.Clamp(pt.Position.X / trackWidth, 0.0, 1.0);
            double targetSeconds = ratio * ViewModel.Transport.TotalDurationSeconds;
            ViewModel.Transport.SeekTo(targetSeconds);
        }
    }

    private void OnWaveformSectionSeekRequested(object? sender, double targetSeconds)
    {
        ViewModel.Transport.SeekTo(targetSeconds);
    }

    public double GetPlayheadX(double currentSecs, double totalSecs)
    {
        if (totalSecs <= 0) return 0;
        double ratio = Math.Clamp(currentSecs / totalSecs, 0.0, 1.0);
        double width = this.ActualWidth > 200 ? this.ActualWidth - 40 : 800;
        return ratio * width;
    }

    public Visibility GetMuteTagVisibility(bool isMuted) => isMuted ? Visibility.Visible : Visibility.Collapsed;

    private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush PadActiveGlow = new(Windows.UI.Color.FromArgb(255, 147, 51, 234)); // Purple Accent #9333EA
    private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush PadInactiveBg = new(Windows.UI.Color.FromArgb(255, 63, 63, 70));

    private void OnPadDockToggleClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (PadDockToggleBtn != null)
        {
            PadDockToggleBtn.Background = PadDockToggleBtn.IsChecked == true ? PadActiveGlow : PadInactiveBg;
        }
    }

    private Microsoft.UI.Xaml.Media.SolidColorBrush GetPadDockBrush(bool isPadActive) => isPadActive ? PadActiveGlow : PadInactiveBg;

    private void OnNavSelected(object? sender, string tab)
    {
        ViewModel.CurrentTab = tab;
        LiveViewGrid.Visibility = tab == "Live" ? Visibility.Visible : Visibility.Collapsed;
        SetlistViewGrid.Visibility = tab == "Setlist" ? Visibility.Visible : Visibility.Collapsed;
        SongsViewGrid.Visibility = tab == "Songs" ? Visibility.Visible : Visibility.Collapsed;
        SettingsViewGrid.Visibility = tab == "Settings" ? Visibility.Visible : Visibility.Collapsed;

        if (TopTransportBar != null)
        {
            TopTransportBar.SetActiveTab(tab);
        }
    }

    private void OnEditToggled(object? sender, bool isEditMode)
    {
        ViewModel.IsEditMode = isEditMode;
    }

    private void OnThemeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeRadioButtons.SelectedItem is RadioButton rb && rb.Tag is string tag)
        {
            var requestedTheme = tag switch
            {
                "Light" => ElementTheme.Light,
                "Dark" => ElementTheme.Dark,
                _ => ElementTheme.Default
            };

            if (this.Content is FrameworkElement root)
            {
                root.RequestedTheme = requestedTheme;
            }
            this.RequestedTheme = requestedTheme;

            if (App.MainWindowInstance?.Content is FrameworkElement windowContent)
            {
                windowContent.RequestedTheme = requestedTheme;
            }

            (App.MainWindowInstance as MainWindow)?.ApplyTitleBarTheme(requestedTheme);
        }
    }

    private void OnLanguageSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LanguageComboBox?.SelectedItem is ComboBoxItem item && item.Tag is string langTag)
        {
            Services.LocalizationService.Instance.CurrentLanguage = langTag;
        }
    }

    private void OnOpenLocalizationFileClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            string path = Services.LocalizationService.Instance.GetEditableFilePath();
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "notepad.exe",
                Arguments = $"\"{path}\"",
                UseShellExecute = true
            });
        }
        catch { }
    }

    private void OnReloadLocalizationClicked(object sender, RoutedEventArgs e)
    {
        Services.LocalizationService.Instance.Reload();
    }

    private void OnOpenTracksFolderClicked(object sender, RoutedEventArgs e)
    {
        OpenFolderInExplorer(ViewModel.DedicatedTracksDirectory);
    }

    private void OnOpenPadsFolderClicked(object sender, RoutedEventArgs e)
    {
        OpenFolderInExplorer(ViewModel.DedicatedPadsDirectory);
    }

    private static void OpenFolderInExplorer(string folderPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(folderPath)) return;
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{folderPath}\"",
                UseShellExecute = true
            });
        }
        catch { }
    }

    private void OnSetlistCardClicked(object? sender, ViewModels.SongItemViewModel song)
    {
        if (ViewModel.IsEditMode) return;
        ViewModel.SelectSong(song);
    }

    private void OnKeyChipClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string keyStr && EasyTracks.Domain.Models.MusicalKeyExtensions.TryParseMusicalKey(keyStr, out var musicalKey))
        {
            ViewModel.Transport.SelectPadKey(musicalKey);
        }
    }

    private async void OnAddSongInEditModeClicked(object sender, RoutedEventArgs e)
    {
        var searchBox = new TextBox
        {
            PlaceholderText = "🔍 Buscar canción en EasyTracks por título, artista o tonalidad...",
            Margin = new Thickness(0, 0, 0, 10)
        };

        var listView = new ListView
        {
            SelectionMode = ListViewSelectionMode.Single,
            MaxHeight = 320,
            MinWidth = 460
        };

        void PopulateList(string query)
        {
            listView.Items.Clear();
            var q = query?.Trim() ?? string.Empty;
            var matches = string.IsNullOrEmpty(q)
                ? ViewModel.AllLibrarySongItems.ToList()
                : ViewModel.AllLibrarySongItems.Where(s =>
                    s.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    s.Artist.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    s.KeyDisplay.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    s.ArrangementDisplay.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();

            foreach (var song in matches)
            {
                var row = new Grid
                {
                    Padding = new Thickness(8, 6, 8, 6),
                    ColumnDefinitions =
                    {
                        new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                        new ColumnDefinition { Width = GridLength.Auto }
                    }
                };

                var leftStack = new StackPanel { Spacing = 2 };
                leftStack.Children.Add(new TextBlock
                {
                    Text = song.Title,
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    FontSize = 13
                });
                leftStack.Children.Add(new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(song.ArrangementDisplay) || song.ArrangementDisplay == "Original"
                        ? song.Artist
                        : $"{song.Artist} • {song.ArrangementDisplay}",
                    FontSize = 11,
                    Opacity = 0.75
                });

                var badgeText = new TextBlock
                {
                    Text = $"{song.KeyDisplay}  •  {song.BpmDisplay} BPM  •  {song.TimeSignatureDisplay}",
                    FontSize = 11,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(12, 0, 0, 0)
                };

                Grid.SetColumn(leftStack, 0);
                Grid.SetColumn(badgeText, 1);
                row.Children.Add(leftStack);
                row.Children.Add(badgeText);

                listView.Items.Add(new ListViewItem
                {
                    Content = row,
                    Tag = song,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch
                });
            }

            if (listView.Items.Count > 0)
            {
                listView.SelectedIndex = 0;
            }
        }

        PopulateList(string.Empty);
        searchBox.TextChanged += (_, _) => PopulateList(searchBox.Text);

        var container = new StackPanel { Spacing = 4, MinWidth = 480 };
        container.Children.Add(searchBox);
        container.Children.Add(listView);

        var dialog = new ContentDialog
        {
            Title = "Agregar Canción al Setlist",
            Content = container,
            PrimaryButtonText = "Agregar al Setlist",
            SecondaryButtonText = "Importar carpeta nueva...",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            if (listView.SelectedItem is ListViewItem selectedItem && selectedItem.Tag is ViewModels.SongItemViewModel chosenSong)
            {
                var existing = ViewModel.SetlistSongs.FirstOrDefault(s => s.Id == chosenSong.Id);
                if (existing == null)
                {
                    existing = new ViewModels.SongItemViewModel(chosenSong.Model, chosenSong.CoverGradientStart, chosenSong.CoverGradientEnd)
                    {
                        IsEditMode = ViewModel.IsEditMode
                    };
                    ViewModel.SetlistSongs.Add(existing);
                    ViewModel.SaveData();
                }
                ViewModel.SelectSong(existing);
            }
        }
        else if (result == ContentDialogResult.Secondary)
        {
            OnImportFolderClicked(sender, e);
        }
    }

    private async void OnCreateNewSetlistClicked(object sender, RoutedEventArgs e)
    {
        var input = new TextBox { Text = $"Servicio {DateTime.Now:dd/MM}" };
        var dialog = new ContentDialog
        {
            Title = "Nuevo Setlist",
            Content = input,
            PrimaryButtonText = "Crear",
            CloseButtonText = "Cancelar",
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(input.Text))
        {
            ViewModel.SetlistManager.CreateNewSetlist(input.Text.Trim());
        }
    }

    private void OnSelectSetlistClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is Setlist setlist)
        {
            ViewModel.SetlistManager.SelectSetlist(setlist);
        }
    }

    private void OnDeleteSetlistClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is Setlist setlist)
        {
            ViewModel.SetlistManager.DeleteSetlist(setlist);
        }
    }

    private void OnSaveSetlistMetaClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.SetlistManager.SyncCurrentSetlistItems();
        ViewModel.SaveData();
    }

    private async void OnImportFolderClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        try
        {
            var picker = new Windows.Storage.Pickers.FolderPicker();
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.MusicLibrary;
            picker.FileTypeFilter.Add("*");

            if (App.MainWindowInstance != null)
            {
                IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            }

            var folder = await picker.PickSingleFolderAsync();
            if (folder != null)
            {
                var importedVm = ViewModel.ImportMultitrackFolder(folder.Path);

                // Immediately open the configuration & metadata dialog
                var configDialog = new Controls.SongConfigDialog(importedVm.Model)
                {
                    XamlRoot = this.XamlRoot
                };

                var configResult = await configDialog.ShowAsync();
                if (configResult == ContentDialogResult.Primary)
                {
                    importedVm.RefreshFromModel();
                    ViewModel.RefreshFilteredSongs();
                    ViewModel.LoadSong(importedVm.Model);
                    ViewModel.SaveData();
                }

                ViewModel.CurrentTab = "Live";
                OnNavSelected(this, "Live");
            }
        }
        catch (Exception ex)
        {
            var dialog = new ContentDialog
            {
                Title = "Error importando secuencia",
                Content = ex.Message,
                CloseButtonText = "Entendido",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
    }

    private void OnSelectSongFromLibraryClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is ViewModels.SongItemViewModel songVm)
        {
            var existingInSetlist = ViewModel.SetlistSongs.FirstOrDefault(s => s.Id == songVm.Id);
            if (existingInSetlist == null)
            {
                existingInSetlist = new ViewModels.SongItemViewModel(songVm.Model, songVm.CoverGradientStart, songVm.CoverGradientEnd);
                ViewModel.SetlistSongs.Add(existingInSetlist);
                ViewModel.SaveData();
            }
            ViewModel.SelectSong(existingInSetlist);
            OnNavSelected(this, "Live");
        }
    }

    private async void OnEditSongMetadataFromRowClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not ViewModels.SongItemViewModel songVm) return;

        var dialog = new Controls.SongConfigDialog(songVm.Model)
        {
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            songVm.RefreshFromModel();
            var inSetlist = ViewModel.SetlistSongs.FirstOrDefault(s => s.Id == songVm.Id);
            inSetlist?.RefreshFromModel();
            ViewModel.RefreshFilteredSongs();

            if (ViewModel.CurrentSong?.Id == songVm.Id)
            {
                ViewModel.LoadSong(songVm.Model);
            }
            ViewModel.SaveData();
        }
    }

    private void OnRemoveSongFromLibraryClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is ViewModels.SongItemViewModel song)
        {
            ViewModel.RemoveSongFromLibrary(song);
        }
    }

    private void OnRemoveSongFromListClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is ViewModels.SongItemViewModel song)
        {
            ViewModel.RemoveSongFromSetlist(song);
        }
    }

    private void OnSetlistCardDeleteClicked(object? sender, ViewModels.SongItemViewModel song)
    {
        ViewModel.RemoveSongFromSetlist(song);
    }

    private async void OnConfigureCurrentSongClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (ViewModel.CurrentSong == null) return;

        var dialog = new Controls.SongConfigDialog(ViewModel.CurrentSong)
        {
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            if (ViewModel.SelectedSong != null)
            {
                ViewModel.SelectedSong.RefreshFromModel();
            }
            var inLib = ViewModel.AllLibrarySongItems.FirstOrDefault(s => s.Id == ViewModel.CurrentSong.Id);
            inLib?.RefreshFromModel();
            ViewModel.RefreshFilteredSongs();
            ViewModel.LoadSong(ViewModel.CurrentSong);
            ViewModel.SaveData();
        }
    }

    private ViewModels.SongItemViewModel? _activeTransitionSong;

    private void OnTransitionButtonClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is ViewModels.SongItemViewModel song)
        {
            _activeTransitionSong = song;
            TransitionPickerFlyout.ShowAt(fe);
        }
    }

    private void OnSelectTransitionTypeClicked(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && _activeTransitionSong != null)
        {
            string tag = item.Tag?.ToString() ?? "";
            _activeTransitionSong.Transition = tag switch
            {
                "ManualStop" => TransitionType.ManualStop,
                "Wait" => TransitionType.Wait,
                "AutoPlay" => TransitionType.AutoPlay,
                "Crossfade" => TransitionType.Crossfade,
                "Hold" => TransitionType.Hold,
                _ => TransitionType.ManualStop
            };
            ViewModel.SaveData();
        }
    }
}
