// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.App;

/// <summary>
/// Unified service contract between MainWindow and its child view controls.
/// Keeps only notifications — font scaling and hotkey capture were removed
/// together with the settings page.
/// </summary>
public interface IAppServices
{
    /// <summary>Show a toast notification with the given severity level.</summary>
    /// <param name="message">Display text.</param>
    /// <param name="level">One of "info", "success", "warning", "error".</param>
    void ShowNotification(string message, string level = "info");
}
