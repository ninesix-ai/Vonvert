// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Vonvert.Engine.AudioEngine;
using Vonvert.App.UIServices;

namespace Vonvert.App;

public partial class ExportDialog : Window
{
    private readonly RecordingHistoryItem _item;
    private readonly LocalizationManager L;
    public ExportOptions Options { get; private set; } = new();
    public string OutputPath { get; private set; } = "";

    public ExportDialog(RecordingHistoryItem item, LocalizationManager loc)
    {
        InitializeComponent();
        _item = item;
        L = loc;

        Title = loc.ExportDialogTitle;
        FilePathBox.Text = item.FilePath;
        UpdateParameterPanel();
    }

    private void FormatChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateParameterPanel();
    }

    private void UpdateParameterPanel()
    {
        var panel = new StackPanel();

        switch (FormatCombo.SelectedIndex)
        {
            case 0: // WAV
                panel.Children.Add(CreateSampleRateSelector());
                panel.Children.Add(CreateBitDepthSelector());
                panel.Children.Add(CreateChannelSelector());
                break;
            case 1: // MP3
                panel.Children.Add(CreateBitrateSelector());
                panel.Children.Add(CreateVBRCheckbox());
                panel.Children.Add(CreateMp3VbrQualitySlider());
                break;
            case 2: // FLAC
                panel.Children.Add(CreateFlacCompressionSlider());
                break;
            case 3: // OGG
                panel.Children.Add(CreateVorbisQualitySlider());
                break;
            case 4: // AAC
                panel.Children.Add(CreateAacBitrateSelector());
                break;
        }

        ParameterPanel.Content = panel;
    }

    private TextBlock Label(string text)
        => new TextBlock
        {
            Text = text,
            Width = 120,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)FindResource("TextPrimary"),
            FontFamily = (FontFamily)FindResource("FontFallback"),
        };

    private UIElement CreateSampleRateSelector()
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
        sp.Children.Add(Label(L.SampleRateLabel));
        var combo = new ComboBox { Width = 120, SelectedIndex = 1 };
        combo.Items.Add("44100 Hz");
        combo.Items.Add("48000 Hz");
        var rates = new int[] { 44100, 48000 };
        combo.SelectionChanged += (s, e) =>
        {
            if (combo.SelectedIndex >= 0 && combo.SelectedIndex < rates.Length)
                Options.SampleRate = rates[combo.SelectedIndex];
        };
        sp.Children.Add(combo);
        return sp;
    }

    private UIElement CreateBitDepthSelector()
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
        sp.Children.Add(Label(L.BitDepthLabel));
        var combo = new ComboBox { Width = 120, SelectedIndex = 2 };
        combo.Items.Add(L.BitDepth16);
        combo.Items.Add(L.BitDepth24);
        combo.Items.Add(L.BitDepth32Float);
        combo.SelectionChanged += (s, e) => Options.BitDepth = combo.SelectedIndex switch { 0 => 16, 1 => 24, _ => 32 };
        sp.Children.Add(combo);
        return sp;
    }

    private UIElement CreateChannelSelector()
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
        sp.Children.Add(Label(L.ChannelsLabel));
        var combo = new ComboBox { Width = 120, SelectedIndex = 0 };
        combo.Items.Add(L.ExportMono);
        combo.Items.Add(L.ExportStereo);
        combo.SelectionChanged += (s, e) => Options.Channels = combo.SelectedIndex == 0 ? 1 : 2;
        sp.Children.Add(combo);
        return sp;
    }

    private UIElement CreateBitrateSelector()
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
        sp.Children.Add(Label(L.BitrateLabel));
        var combo = new ComboBox { Width = 120, SelectedIndex = 2 };
        combo.Items.Add("64");
        combo.Items.Add("96");
        combo.Items.Add("128");
        combo.Items.Add("192");
        combo.Items.Add("256");
        combo.Items.Add("320");
        int[] bitrates = { 64, 96, 128, 192, 256, 320 };
        combo.SelectionChanged += (s, e) => { if (combo.SelectedIndex >= 0) Options.Bitrate = bitrates[combo.SelectedIndex]; };
        sp.Children.Add(combo);
        return sp;
    }

    private UIElement CreateVBRCheckbox()
    {
        var cb = new CheckBox { Content = L.VbrModeLabel, Margin = new Thickness(124, 4, 0, 4) };
        cb.Checked += (s, e) => Options.UseVBR = true;
        cb.Unchecked += (s, e) => Options.UseVBR = false;
        return cb;
    }

    private UIElement CreateMp3VbrQualitySlider()
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
        sp.Children.Add(Label(L.VbrQualityLabel));
        var slider = new Slider { Minimum = 0, Maximum = 1, Value = 0.5, Width = 120 };
        var label = new TextBlock { Text = "0.5", Width = 30, VerticalAlignment = VerticalAlignment.Center };
        slider.ValueChanged += (s, e) => { Options.VorbisQuality = (float)slider.Value; label.Text = slider.Value.ToString("F1"); };
        sp.Children.Add(slider);
        sp.Children.Add(label);
        return sp;
    }

    private UIElement CreateFlacCompressionSlider()
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
        sp.Children.Add(Label(L.CompressionLabel));
        var slider = new Slider { Minimum = 0, Maximum = 8, Value = 5, Width = 120, IsSnapToTickEnabled = true, TickFrequency = 1 };
        var label = new TextBlock { Text = "5", Width = 30, VerticalAlignment = VerticalAlignment.Center };
        slider.ValueChanged += (s, e) => { Options.FlacCompression = (int)slider.Value; label.Text = slider.Value.ToString(); };
        sp.Children.Add(slider);
        sp.Children.Add(label);
        return sp;
    }

    private UIElement CreateVorbisQualitySlider()
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
        sp.Children.Add(Label(L.QualityLabel));
        var slider = new Slider { Minimum = 0, Maximum = 1, Value = 0.5, Width = 120 };
        var label = new TextBlock { Text = "0.5", Width = 30, VerticalAlignment = VerticalAlignment.Center };
        slider.ValueChanged += (s, e) => { Options.VorbisQuality = (float)slider.Value; label.Text = slider.Value.ToString("F1"); };
        sp.Children.Add(slider);
        sp.Children.Add(label);
        return sp;
    }

    private UIElement CreateAacBitrateSelector()
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
        sp.Children.Add(Label(L.BitrateLabel));
        var combo = new ComboBox { Width = 120, SelectedIndex = 2 };
        combo.Items.Add("64");
        combo.Items.Add("96");
        combo.Items.Add("128");
        combo.Items.Add("192");
        combo.Items.Add("256");
        int[] bitrates = { 64, 96, 128, 192, 256 };
        combo.SelectionChanged += (s, e) => { if (combo.SelectedIndex >= 0) Options.AacBitrate = bitrates[combo.SelectedIndex]; };
        sp.Children.Add(combo);
        return sp;
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        Options.Format = FormatCombo.SelectedIndex switch
        {
            0 => ExportFormat.Wav,
            1 => ExportFormat.Mp3,
            2 => ExportFormat.Flac,
            3 => ExportFormat.Ogg,
            4 => ExportFormat.Aac,
            _ => ExportFormat.Wav
        };

        string ext = Options.Format switch
        {
            ExportFormat.Wav => ".wav",
            ExportFormat.Mp3 => ".mp3",
            ExportFormat.Flac => ".flac",
            ExportFormat.Ogg => ".ogg",
            ExportFormat.Aac => ".m4a",
            _ => ".wav"
        };

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = L.ExportRecording,
            Filter = $"{Options.Format} Files (*{ext})|*{ext}|{L.AllFiles} (*.*)|*.*",
            FileName = System.IO.Path.ChangeExtension(_item.FileName, ext),
            DefaultExt = ext
        };

        if (dlg.ShowDialog() == true)
        {
            OutputPath = dlg.FileName;
            DialogResult = true;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
