// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.App.UIServices;

public partial class LocalizationManager
{
    // ── About tab / version info ──
    public string NavAbout    => G();
    public string About       => G();
    public string CopyAll     => G();
    public string Copied      => G();
    public string CopyFailed  => G();

    // ── Help links (open the published guides in the browser) ──
    public string AboutGuides           => G();
    public string AboutUserGuideLink    => G();
    public string AboutMonitorGuideLink => G();

    // ── Third-party licenses & attribution ──
    public string ThirdPartyLicenses => G();
    public string LicenseIntro       => G();
    public string Trademarks         => G();
    // Each bullet is one localized line; the library names and the SPDX-style
    // license ids (MIT / Apache-2.0) stay verbatim inside every language's value -
    // only the trailing descriptor ("— .NET audio library" …) is translated.
    public string LicAudio   => G();
    public string LicJson    => G();
    public string LicLogging => G();
    public string LicTray    => G();
    public string LicRuntime => G();
    public string LicVbCable => G();
}
