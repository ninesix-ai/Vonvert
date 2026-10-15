// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio
namespace Vonvert.App.UIServices;

public partial class LocalizationManager
{
    // ── First-run legal consent dialog ──
    public string LegalTitle          => G();
    public string LegalIntro          => G();
    public string LegalProhibitedHead => G();
    public string LegalBullets        => G();   // newline-separated
    public string LegalPrivacyHead    => G();
    public string LegalInfo           => G();   // newline-separated
    public string LegalReadFull       => G();
    public string LegalPrivacyLink    => G();
    public string LegalTermsLink      => G();
    public string LegalAck            => G();
    public string LegalAgree          => G();
    public string LegalDecline        => G();
}
