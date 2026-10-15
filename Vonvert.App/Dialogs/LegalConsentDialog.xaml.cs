// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Vonvert.App.UIServices;

namespace Vonvert.App;

/// <summary>
/// First-run modal: shows localized prohibited-use bullets + privacy facts, links to
/// the full Privacy/Terms docs (#4), and requires an explicit acknowledge + Agree.
/// DialogResult true = accepted; false (Decline, or closing the window) = declined.
/// </summary>
public partial class LegalConsentDialog : Window
{
    private readonly LocalizationManager _loc;

    public LegalConsentDialog(LocalizationManager loc)
    {
        InitializeComponent();
        _loc = loc;
        Title = loc.LegalTitle;
        TitleText.Text = loc.LegalTitle;
        ReadFullText.Text = loc.LegalReadFull;
        PrivacyLink.Inlines.Add(new Run(loc.LegalPrivacyLink));
        TermsLink.Inlines.Add(new Run(loc.LegalTermsLink));
        AckCheck.Content = loc.LegalAck;
        AgreeBtn.Content = loc.LegalAgree;
        DeclineBtn.Content = loc.LegalDecline;

        ContentHost.Children.Add(new TextBlock { Text = loc.LegalIntro, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) });
        AddHead(loc.LegalProhibitedHead);
        AddBullets(loc.LegalBullets);
        AddHead(loc.LegalPrivacyHead);
        AddBullets(loc.LegalInfo);
    }

    private void AddHead(string t)
        => ContentHost.Children.Add(new TextBlock { Text = t, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 6, 0, 4) });

    // Bullets arrive as one newline-separated localized string.
    private void AddBullets(string block)
    {
        foreach (var line in block.Split('\n'))
            if (!string.IsNullOrWhiteSpace(line))
                ContentHost.Children.Add(new TextBlock { Text = line.Trim(), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 3) });
    }

    private void Ack_Changed(object s, RoutedEventArgs e) => AgreeBtn.IsEnabled = AckCheck.IsChecked == true;
    private void Agree_Click(object s, RoutedEventArgs e) { DialogResult = true; Close(); }
    private void Decline_Click(object s, RoutedEventArgs e) { DialogResult = false; Close(); }
    private void Privacy_Click(object s, RoutedEventArgs e) => OpenDoc(DocsLinks.PrivacyPolicyUrl(_loc.Language));
    private void Terms_Click(object s, RoutedEventArgs e) => OpenDoc(DocsLinks.TermsOfServiceUrl(_loc.Language));

    private static void OpenDoc(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { /* opening the browser is best-effort */ }
    }
}
