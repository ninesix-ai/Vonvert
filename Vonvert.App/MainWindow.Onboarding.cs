// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Vonvert.App.OnboardingSteps;
using Vonvert.Engine;
using Vonvert.Engine.AudioEngine;
using Vonvert.Engine.Services;

namespace Vonvert.App;

/// <summary>
/// First-run experience in two parts: (1) a short setup wizard
/// (Welcome -> Role -> Devices -> Done) that captures the role and devices, and
/// (2) a <see cref="Controls.TourOverlay"/> spotlight tour that walks the user over
/// the main window, highlighting the real controls and what each does. Each part has
/// its own "done" marker so it runs once per install. No telemetry, no gating.
/// </summary>
public partial class MainWindow
{
    private const int WizardSteps = 4;
    private static string OnboardingMarkerPath => Path.Combine(AppPaths.Root, "onboarding_done");

    private void MaybeRunOnboarding()
    {
        try
        {
            if (!File.Exists(OnboardingMarkerPath))
            {
                var (completed, _) = ShowOnboardingWizard();
                try
                {
                    Directory.CreateDirectory(AppPaths.Root);
                    File.WriteAllText(OnboardingMarkerPath, completed ? "completed" : "skipped");
                }
                catch (Exception ex) { AppLog.Warning(ex, "Onboarding marker write failed"); }
            }
            else
            {
                Role.HasCompletedOnboarding = true;
            }
        }
        catch (Exception ex) { AppLog.Warning(ex, "MaybeRunOnboarding failed"); }
        finally { ScheduleFirstRunTour(); }   // then spotlight the main window features
    }

    // ── Setup wizard ──────────────────────────────────────────────────
    private (bool Completed, bool DevicesChanged) ShowOnboardingWizard()
    {
        try
        {
            var dlg = new Window
            {
                Title = L.OnbWelcomeTitle, Width = 560, Height = 540,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, Topmost = true, Owner = this,
            };
            var grid = new Grid { Margin = new Thickness(20) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var progress = new TextBlock { FontSize = 11, HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetRow(progress, 0); grid.Children.Add(progress);
            var title = new TextBlock { FontSize = 17, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 4, 0, 10) };
            Grid.SetRow(title, 1); grid.Children.Add(title);
            var body = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) };
            var contentHost = new ContentControl { VerticalAlignment = VerticalAlignment.Top };
            var bodyPanel = new StackPanel(); bodyPanel.Children.Add(body); bodyPanel.Children.Add(contentHost);
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = bodyPanel };
            Grid.SetRow(scroll, 2); grid.Children.Add(scroll);

            // ── Device step: combos + refresh + plain-language guidance ──
            var micLabel = new TextBlock { Text = L.InputMic, FontSize = 11, Margin = new Thickness(0, 0, 0, 4) };
            var micCombo = new ComboBox { MinWidth = 300, MaxWidth = 420, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 10) };
            var outLabel = new TextBlock { Text = L.OutputVbCable, FontSize = 11, Margin = new Thickness(0, 0, 0, 4) };
            var outCombo = new ComboBox { MinWidth = 300, MaxWidth = 420, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 10) };
            var refreshBtn = new Button { Content = L.RefreshDevices, HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(0, 0, 0, 10) };
            var hint = new TextBlock { Text = L.DeviceHint, FontSize = 11, TextWrapping = TextWrapping.Wrap, Opacity = 0.8, Margin = new Thickness(0, 4, 0, 0) };
            var devicesPanel = new StackPanel();
            devicesPanel.Children.Add(micLabel);
            devicesPanel.Children.Add(micCombo);
            devicesPanel.Children.Add(outLabel);
            devicesPanel.Children.Add(outCombo);
            devicesPanel.Children.Add(refreshBtn);
            devicesPanel.Children.Add(hint);

            void PopulateDeviceCombos()
            {
                try
                {
                    var inputs = App.Devices.InputDevices();
                    micCombo.ItemsSource = inputs; micCombo.DisplayMemberPath = "Name";
                    if (inputs.Count > 0)
                    {
                        var defIn = DeviceSelection.ResolveInput(inputs, App.Engine.Settings.InputDeviceId,
                            App.Devices.DefaultInputDevice()?.ID);
                        if (defIn != null) micCombo.SelectedItem = defIn; else micCombo.SelectedIndex = 0;
                    }
                    else { micCombo.ItemsSource = new[] { new AudioDevice(string.Empty, L.NoMicFound, 0, 0, 0) }; }

                    var outputs = App.Devices.OutputDevices();
                    outCombo.ItemsSource = outputs; outCombo.DisplayMemberPath = "Name";
                    if (outputs.Count > 0)
                    {
                        var defOut = outputs.FirstOrDefault(d => d.Id == App.Engine.Settings.OutputDeviceId)
                            ?? outputs.FirstOrDefault(d => d.Name != null && d.Name.Contains("CABLE", StringComparison.OrdinalIgnoreCase))
                            ?? outputs.FirstOrDefault();
                        if (defOut != null) outCombo.SelectedItem = defOut;
                    }
                    else { outCombo.ItemsSource = new[] { new AudioDevice(string.Empty, L.NoMicFound, 0, 0, 0) }; }
                }
                catch (Exception ex) { AppLog.Warning(ex, "Onboarding device populate failed"); }
            }
            PopulateDeviceCombos();
            refreshBtn.Click += (_, _) => PopulateDeviceCombos();   // user can plug in a mic/cable then refresh

            var back = new Button { Content = L.OnbBack, Padding = new Thickness(16, 6, 16, 6), Margin = new Thickness(0, 0, 8, 0) };
            var skip = new Button { Content = L.OnbSkip, Padding = new Thickness(16, 6, 16, 6), Margin = new Thickness(0, 0, 8, 0) };
            var next = new Button { Content = L.OnbNext, Padding = new Thickness(16, 6, 16, 6) };
            var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            btns.Children.Add(back); btns.Children.Add(skip); btns.Children.Add(next);
            Grid.SetRow(btns, 3); grid.Children.Add(btns);

            int step = 0; PersonaType? chosen = null; bool skipped = false; bool devicesChanged = false;
            PersonaPickStep? pick = null;

            void Render()
            {
                progress.Text = string.Format(L.OnbStepProgress, step + 1, WizardSteps);
                back.Visibility = step == 0 ? Visibility.Collapsed : Visibility.Visible;
                skip.Visibility = step == WizardSteps - 1 ? Visibility.Collapsed : Visibility.Visible;
                next.Content = step == WizardSteps - 1 ? L.OnbGetStarted : L.OnbNext;
                switch (step)
                {
                    case 0: title.Text = L.OnbWelcomeTitle; body.Text = L.OnbWelcomeBody; contentHost.Content = null; break;
                    case 1:
                        title.Text = L.OnbStepPersonaTitle; body.Text = L.OnbStepPersonaBody;
                        if (pick == null) pick = new PersonaPickStep();   // SelectionChanged forwards to chosen below
                        pick.SelectionChanged += p => chosen = p;
                        contentHost.Content = pick; break;
                    case 2: title.Text = L.OnbStepDevicesTitle; body.Text = L.OnbStepDevicesBody; contentHost.Content = devicesPanel; break;
                    default: title.Text = L.OnbStepDoneTitle; body.Text = L.OnbStepDoneBody; contentHost.Content = null; break;
                }
            }

            void ApplyDevices()
            {
                if (micCombo.SelectedItem is AudioDevice di && !string.IsNullOrEmpty(di.Id) && App.Engine.Settings.InputDeviceId != di.Id)
                { App.Engine.Settings.InputDeviceId = di.Id; devicesChanged = true; }
                if (outCombo.SelectedItem is AudioDevice dov && !string.IsNullOrEmpty(dov.Id) && App.Engine.Settings.OutputDeviceId != dov.Id)
                { App.Engine.Settings.OutputDeviceId = dov.Id; devicesChanged = true; }
            }

            back.Click += (_, _) => { if (step > 0) { step--; Render(); } };
            next.Click += (_, _) =>
            {
                if (step == 2) ApplyDevices();
                if (step < WizardSteps - 1) { step++; Render(); return; }
                dlg.DialogResult = true; dlg.Close();
            };
            skip.Click += (_, _) => { skipped = true; dlg.DialogResult = false; dlg.Close(); };

            dlg.Content = grid;
            Render();
            dlg.ShowDialog();

            var outcome = dlg.DialogResult == true ? OnboardingOutcome.Completed
                        : skipped ? OnboardingOutcome.Skipped : OnboardingOutcome.Cancelled;
            OnboardingFlow.Apply(outcome, chosen, Role);
            ApplyRecommendedPresetForActiveRole();
            try { PopulateDevices(); } catch (Exception ex) { AppLog.Warning(ex, "Onboarding PopulateDevices failed"); }
            return (dlg.DialogResult == true, devicesChanged);
        }
        catch (Exception ex) { AppLog.Warning(ex, "Onboarding wizard failed"); return (false, false); }
    }

    // ── Spotlight tour over the main window ───────────────────────────
    private void ScheduleFirstRunTour(int delayMs = 900)
    {
        if (Controls.TourOverlay.TourCompletedStatic) return;

        void Begin()
        {
            var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(delayMs) };
            t.Tick += (_, _) =>
            {
                t.Stop();
                if (Controls.TourOverlay.TourCompletedStatic) return;
                try
                {
                    MainTabs.SelectedIndex = 0;
                    TourOverlay.StepShowing += Tour_StepShowing;
                    TourOverlay.TourCompleted += Tour_Completed;
                    TourOverlay.StartTour(new FrameworkElement[]
                    {
                        MainPresetPicker,                 // 1. pick a voice
                        BottomBar?.VoiceToggleBtn!,        // 2. power: turn voice change on/off
                        ExpertPanel,                       // 3. tune parameters
                        MicSelector,                       // 4. audio devices (settings)
                    });
                }
                catch (Exception ex) { AppLog.Warning(ex, "StartTour failed"); }
            };
            t.Start();
        }

        if (IsLoaded) Begin();
        else Loaded += (_, _) => Begin();
    }

    private void Tour_StepShowing(int idx)
    {
        // Device step lives on the Settings tab; the parameter panel is a slide-out rail
        // that must be expanded to have a measurable size. Everything else is on Voices.
        if (idx == 3) { MainTabs.SelectedIndex = 1; CollapseParamPanel(); return; }
        MainTabs.SelectedIndex = 0;
        if (idx == 2) ExpandParamPanel(); else CollapseParamPanel();
    }

    private void Tour_Completed()
    {
        TourOverlay.StepShowing -= Tour_StepShowing;
        TourOverlay.TourCompleted -= Tour_Completed;
        try { MainTabs.SelectedIndex = 0; CollapseParamPanel(); } catch { /* restore is best-effort */ }
    }
}
