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

    // ── Third-party licenses & attribution ──
    public string ThirdPartyLicenses => G();
    public string LicenseIntro       => G();
    public string Trademarks         => G();
}
