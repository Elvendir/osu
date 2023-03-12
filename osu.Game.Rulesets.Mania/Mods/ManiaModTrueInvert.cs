// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Mods
{
    internal class ManiaModTrueInvert : Mod, IApplicableAfterBeatmapConversion
    {
        public override string Name => "True Invert";

        public override string Acronym => "TIN";
        public override double ScoreMultiplier => 1;

        public override LocalisableString Description => "Release the keys. To the beat.";

        public override IconUsage? Icon => FontAwesome.Solid.YinYang;

        public override ModType Type => ModType.Conversion;

        public override Type[] IncompatibleMods => new[] { typeof(ManiaModHoldOff), typeof(ManiaModInvert) };

        [SettingSource("Release Space", "Fraction of a beat let to before pressing the next note.")]
        public BindableNumber<int> SpaceBeat { get; } = new BindableInt(1)
        {
            MinValue = 1,
            MaxValue = 16,
            Default = 4,
            Value = 4
        };


        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            var maniaBeatmap = (ManiaBeatmap)beatmap;

            var newObjects = new List<ManiaHitObject>();

            var allowedTypes = new[] { typeof(Note), typeof(HoldNote) };

            foreach (var column in maniaBeatmap.HitObjects.GroupBy(h => h.Column))
            {
                var newColumnObjects = new List<ManiaHitObject>();

                var locations = column.Where(item => allowedTypes.Contains(item.GetType())).OrderBy(h => h.StartTime).ToList();

                for (int i = 0; i < locations.Count - 1; i++)
                {
                    double duration;
                    double beatLength;

                    if (locations[i] is Note)
                    {
                        // Beat length at the end of the hold note.
                        beatLength = beatmap.ControlPointInfo.TimingPointAt(locations[i + 1].StartTime).BeatLength;

                        // Determining HoldNotes duration
                        duration = locations[i + 1].StartTime - locations[i].StartTime;
                        duration = duration = Math.Max(duration / 2, duration - beatLength / SpaceBeat.Value);

                        newColumnObjects.Add(new HoldNote
                        {
                            Column = column.Key,
                            StartTime = locations[i + 1].StartTime - duration,
                            Duration = duration,
                            NodeSamples = new List<IList<HitSampleInfo>> { Array.Empty<HitSampleInfo>(), locations[i + 1].Samples }
                        });
                    }

                    if (locations[i] is HoldNote loclocation)
                    {
                        newColumnObjects.Add(new HoldNote
                        {
                            Column = column.Key,
                            StartTime = loclocation.EndTime,
                            Duration = locations[i + 1].StartTime - loclocation.EndTime,
                            NodeSamples = new List<IList<HitSampleInfo>> { loclocation.GetNodeSamples(1), locations[i + 1].Samples }
                        });
                    }
                }

                // Adds a Note if last HitObjects is a HoldNote
                if (locations.Last() is HoldNote loclocation2)
                {
                    newColumnObjects.Add(new Note
                    {
                        Column = column.Key,
                        StartTime = loclocation2.EndTime,
                        Samples = loclocation2.GetNodeSamples(1)
                    });
                }

                newObjects.AddRange(newColumnObjects);
            }

            maniaBeatmap.HitObjects = newObjects.OrderBy(h => h.StartTime).ToList();

            // No breaks
            maniaBeatmap.Breaks.Clear();
        }
    }
}
