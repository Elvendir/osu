// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Replays;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Mods
{
    public class ManiaModAutoplay : ModAutoplay
    {

        [SettingSource("PlayerLevel of Autoplay", "Set to negative to get osu!topus")]
        public BindableNumber<double> PlayerLevel { get; } = new BindableDouble(-1)
        {
            MinValue = -1,
            MaxValue = 20,
            Precision = 0.1,
            Default = -1,
        };
        public override ModReplayData CreateReplayData(IBeatmap beatmap, IReadOnlyList<Mod> mods)
        {
            var autoPlay = new ManiaAutoGenerator((ManiaBeatmap)beatmap).Generate();
            var user = new ModCreatedUser { Username = "osu!topus" };
            if (PlayerLevel.Value >= 0)
            {
                autoPlay = new ManiaPlayerGenerator((ManiaBeatmap)beatmap, PlayerLevel.Value).Generate();
                user = new ModCreatedUser { Username = $"PlayerLevel: {PlayerLevel}" };
            }
            return new ModReplayData(autoPlay, user);

        }
    }
}
