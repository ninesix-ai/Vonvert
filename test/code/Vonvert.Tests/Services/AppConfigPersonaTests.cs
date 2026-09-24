// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio
using System.Collections.Generic;
using Newtonsoft.Json;
using Vonvert.Engine.Services;
using Xunit;

namespace Vonvert.Tests.Services;

public class AppConfigPersonaTests
{
    [Fact]
    public void PersonaSection_Defaults_Are_Empty_And_NullOverride()
    {
        var s = new AppConfig.PersonaSection();
        Assert.Empty(s.ActivePersonas);
        Assert.Null(s.ComplexityLevelOverride);
    }

    [Fact]
    public void PersonaSection_RoundTrips_Through_Json()
    {
        var s = new AppConfig.PersonaSection { ActivePersonas = new List<string> { "Gamer", "Streamer" }, ComplexityLevelOverride = "Advanced" };
        var back = JsonConvert.DeserializeObject<AppConfig.PersonaSection>(JsonConvert.SerializeObject(s))!;
        Assert.Equal(new[] { "Gamer", "Streamer" }, back.ActivePersonas);
        Assert.Equal("Advanced", back.ComplexityLevelOverride);
    }
}
