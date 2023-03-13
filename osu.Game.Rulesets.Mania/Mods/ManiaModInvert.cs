// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Mods
{
    public class ManiaModInvert : Mod, IApplicableAfterBeatmapConversion
    {
        public override string Name => "Invert";

        public override string Acronym => "IN";
        public override double ScoreMultiplier => 1;

        public override LocalisableString Description => "Hold the keys. To the beat.";

        public override IconUsage? Icon => FontAwesome.Solid.YinYang;

        public override ModType Type => ModType.Conversion;

        public override Type[] IncompatibleMods => new[] { typeof(ManiaModHoldOff) };

        private const int float_error_leniency = 128;

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            var maniaBeatmap = (ManiaBeatmap)beatmap;

            var newObjects = new List<ManiaHitObject>();

            var allowedTypes = new[] { typeof(Note), typeof(HoldNote) };

            foreach (var column in maniaBeatmap.HitObjects.GroupBy(h => h.Column))
            {
                var newColumnObjects = new List<ManiaHitObject>();

                //Select only Note and HoldNote in HitObjects
                var locations = column.Where(item => allowedTypes.Contains(item.GetType())).OrderBy(h => h.StartTime).ToList();

                for (int i = 0; i < locations.Count - 1; i++)
                {
                    // If Note might create HoldNote
                    if (locations[i] is Note)
                    {
                        // Beat length at the end of the hold note.
                        double beatLength = beatmap.ControlPointInfo.TimingPointAt(locations[i + 1].StartTime).BeatLength;

                        // If HoldNote created, duration is Time to next Note - 1/4 of a beat
                        double duration = locations[i + 1].StartTime - locations[i].StartTime - beatLength / 4;

                        // If duration is shorter than 1/8 of a beat keep the Note
                        // Error leniency on float when duration = 1/8 of a beat
                        if (duration < beatLength / 8 - beatLength / float_error_leniency)
                        {
                            newColumnObjects.Add(locations[i]);
                        }
                        else
                        {
                            newColumnObjects.Add(new HoldNote
                            {
                                Column = column.Key,
                                StartTime = locations[i].StartTime,
                                Duration = duration,
                                NodeSamples = new List<IList<HitSampleInfo>> { locations[i].Samples, Array.Empty<HitSampleInfo>() }
                            });
                        }
                    }
                    // Conserves HoldNote as is
                    else
                    {
                        newColumnObjects.Add(locations[i]);
                    }
                }

                // Adds last HitObject in column
                newColumnObjects.Add(locations.Last());

                newObjects.AddRange(newColumnObjects);
            }

            maniaBeatmap.HitObjects = newObjects.OrderBy(h => h.StartTime).ToList();

            // No breaks
            maniaBeatmap.Breaks.Clear();
        }
    }
}
