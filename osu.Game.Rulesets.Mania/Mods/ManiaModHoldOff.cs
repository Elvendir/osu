// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Bindables;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;
using osu.Framework.Graphics.Sprites;
using System.Collections.Generic;
using osu.Framework.Localisation;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Audio;

namespace osu.Game.Rulesets.Mania.Mods
{
    public class ManiaModHoldOff : Mod, IApplicableAfterBeatmapConversion
    {
        public override string Name => "Hold Off";

        public override string Acronym => "HO";

        public override double ScoreMultiplier => 0.9;

        public override LocalisableString Description => @"Replaces all hold notes with normal notes.";

        public override IconUsage? Icon => FontAwesome.Solid.DotCircle;

        public override ModType Type => ModType.Conversion;

        public override Type[] IncompatibleMods => new[] { typeof(ManiaModInvert) };

        [SettingSource("Releases are Notes too", "Converts end of HoldNotes as Notes.")]
        public BindableBool ConvertReleases { get; } = new BindableBool(false);

        [SettingSource("More rice please.", "Converts HoldNotes into consecutives Notes. Notes are separated by BeatLength / Value.")]
        public BindableNumber<int> SpaceBeat { get; } = new BindableInt(1)
        {
            MinValue = 0,
            MaxValue = 16,
            Default = 0,
            Value = 0
        };

        [SettingSource("But not too much.", "Notes are separated by BeatLength * TimeSignature / Value instead.")]
        public BindableBool LessNotes { get; } = new BindableBool(false);


        [SettingSource("And without sound.", "Activates/Deactivates HitSounds for added extra Notes.")]
        public BindableBool NoExtraSound { get; } = new BindableBool(false);

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            var maniaBeatmap = (ManiaBeatmap)beatmap;

            var newObjects = new List<ManiaHitObject>();

            foreach (var h in beatmap.HitObjects.OfType<HoldNote>())
            {
                // Add a note for the beginning of the hold note
                newObjects.Add(new Note
                {
                    Column = h.Column,
                    StartTime = h.StartTime,
                    Samples = h.GetNodeSamples(0)
                });

                // Add a note for the end of the hold note if asked
                if (ConvertReleases.Value)
                {
                    newObjects.Add(new Note
                    {
                        Column = h.Column,
                        StartTime = h.EndTime,
                        Samples = h.GetNodeSamples((h.NodeSamples?.Count - 1) ?? 1)
                    });
                }

                // Add a note every 1 / SpaceBeat
                if (SpaceBeat.Value > 0)
                {
                    int signature = beatmap.ControlPointInfo.TimingPointAt(h.StartTime).TimeSignature.Numerator;
                    double noteSeparation = beatmap.ControlPointInfo.TimingPointAt(h.StartTime).BeatLength / SpaceBeat.Value;
                    if (LessNotes.Value)
                    {
                        noteSeparation *= signature;
                    }
                    for (double startTime = h.StartTime + noteSeparation; startTime < h.EndTime; startTime += noteSeparation)
                    {
                        if (NoExtraSound.Value)
                        {
                            newObjects.Add(new Note
                            {
                                Column = h.Column,
                                StartTime = startTime,
                                Samples = Array.Empty<HitSampleInfo>()
                            });
                        }
                        else
                        {
                            newObjects.Add(new Note
                            {
                                Column = h.Column,
                                StartTime = startTime,
                                Samples = h.GetNodeSamples(0)
                            });
                        }
                    }
                }
            }
            maniaBeatmap.HitObjects = maniaBeatmap.HitObjects.OfType<Note>().Concat(newObjects).OrderBy(h => h.StartTime).ToList();
        }
    }
}
