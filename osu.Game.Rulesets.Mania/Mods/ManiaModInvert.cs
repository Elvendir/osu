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
    public class ManiaModInvert : Mod, IApplicableAfterBeatmapConversion
    {
        public override string Name => "Invert";

        public override string Acronym => "IN";
        public override double ScoreMultiplier => 1;

        public override LocalisableString Description => "Hold the keys. To the beat.";

        public override IconUsage? Icon => FontAwesome.Solid.YinYang;

        public override ModType Type => ModType.Conversion;

        public override Type[] IncompatibleMods => new[] { typeof(ManiaModHoldOff) };

        [SettingSource("Release Space", "Fraction of a beat let to release the note.")]
        public BindableNumber<int> SpaceBeat { get; } = new BindableInt(1)
        {
            MinValue = 1,
            MaxValue = 16,
            Default = 4,
            Value = 4
        };

        [SettingSource("Shortest HoldNote duration", "Fraction of beat under which an HoldNote becomes a Note.")]
        public BindableNumber<int> ShortestBeat { get; } = new BindableInt(1)
        {
            MinValue = 1,
            MaxValue = 32,
            Default = 8,
            Value = 8
        };

        [SettingSource("Type of HoldNotes convertion", "0:Deleted. 1:Conserved. 2:Extended. 3:Inverted.")]
        public BindableNumber<int> TypeHoldNoteConversion { get; } = new BindableInt(1)
        {
            MinValue = 0,
            MaxValue = 3,
            Default = 2,
            Value = 2
        };

        [SettingSource("Converion for short HoldNotes", "1:Note. 2:Half-Length. 3:Constant.")]
        public BindableNumber<int> TypeShortHoldNoteConversion { get; } = new BindableInt(1)
        {
            MinValue = 1,
            MaxValue = 3,
            Default = 2,
            Value = 2
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
                        duration = durationcalculation(locations[i + 1].StartTime - locations[i].StartTime, beatLength);

                        // If duration is shorter than shortest one requested make it a Note
                        if (duration < beatLength / ShortestBeat.Value - beatLength / float_error_leniency)
                        {
                            newColumnObjects.Add(new Note
                            {
                                Column = column.Key,
                                StartTime = locations[i].StartTime,
                                Samples = locations[i].Samples
                            });
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

                    if (locations[i] is HoldNote loclocation)
                    {
                        switch (TypeHoldNoteConversion.Value)
                        {
                            // Deletes HoldNotes
                            case 0:
                                break;

                            // Conserves HoldNotes as is
                            case 1:
                                newColumnObjects.Add(locations[i]);
                                break;

                            // Completes HoldNotes by extending duration to next HitObject
                            case 2:
                                // Beat length at the end of the hold note.
                                beatLength = beatmap.ControlPointInfo.TimingPointAt(locations[i + 1].StartTime).BeatLength;

                                // Determining HoldNotes duration
                                duration = durationcalculation(locations[i + 1].StartTime - loclocation.StartTime, beatLength);

                                // If duration is shorter than shortest one requested make it a Note
                                if (duration < beatLength / ShortestBeat.Value - beatLength / float_error_leniency)
                                {
                                    newColumnObjects.Add(new Note
                                    {
                                        Column = column.Key,
                                        StartTime = loclocation.StartTime,
                                        Samples = loclocation.GetNodeSamples(0)
                                    });
                                }
                                else
                                {
                                    newColumnObjects.Add(new HoldNote
                                    {
                                        Column = column.Key,
                                        StartTime = loclocation.StartTime,
                                        Duration = duration,
                                        NodeSamples = new List<IList<HitSampleInfo>> { loclocation.GetNodeSamples(0), Array.Empty<HitSampleInfo>() }
                                    });
                                }

                                break;

                            // Inverses HoldNotes
                            case 3:
                                // Beat length at the end of the hold note.
                                beatLength = beatmap.ControlPointInfo.TimingPointAt(locations[i + 1].StartTime).BeatLength;

                                // Determining HoldNotes duration
                                duration = durationcalculation(locations[i + 1].StartTime - loclocation.EndTime, beatLength);

                                // If duration is shorter than shortest one requested make it a Note
                                if (duration < beatLength / ShortestBeat.Value - beatLength / float_error_leniency)
                                {
                                    newColumnObjects.Add(new Note
                                    {
                                        Column = column.Key,
                                        StartTime = loclocation.EndTime,
                                        Samples = loclocation.GetNodeSamples(1)
                                    });
                                }
                                else
                                {
                                    newColumnObjects.Add(new HoldNote
                                    {
                                        Column = column.Key,
                                        StartTime = loclocation.EndTime,
                                        Duration = duration,
                                        NodeSamples = new List<IList<HitSampleInfo>> { loclocation.GetNodeSamples(1), Array.Empty<HitSampleInfo>() }
                                    });
                                }

                                break;
                        }
                    }
                }

                // Adds last HitObject in column
                if (locations.Last() is Note)
                {
                    newColumnObjects.Add(locations.Last());
                }

                if (locations.Last() is HoldNote loclocation2)
                {
                    switch (TypeHoldNoteConversion.Value)
                    {
                        // Deletes HoldNotes
                        case 0:
                            break;

                        case 1:
                            newColumnObjects.Add(loclocation2);
                            break;

                        case 2:
                            newColumnObjects.Add(loclocation2);
                            break;

                        case 3:
                            newColumnObjects.Add(new Note
                            {
                                Column = column.Key,
                                StartTime = loclocation2.EndTime,
                                Samples = loclocation2.GetNodeSamples(1)
                            });
                            break;
                    }
                }

                newObjects.AddRange(newColumnObjects);
            }

            maniaBeatmap.HitObjects = newObjects.OrderBy(h => h.StartTime).ToList();

            // No breaks
            maniaBeatmap.Breaks.Clear();
        }

        private const int float_error_leniency = 128;

        private double durationcalculation(double fullduration, double beatLength)
        {
            // Full duration of the hold note.
            double duration = fullduration;

            // Decrease the duration by spacing requested.
            switch (TypeShortHoldNoteConversion.Value)
            {
                // Always decreases by requested spacing
                case 1:
                    duration -= beatLength / SpaceBeat.Value;
                    break;

                // Decreases by requested spacing until (duration < Time_between_notes / 2)
                // Afterward, takes duration = Time_between_notes / 2
                case 2:
                    duration = Math.Max(duration / 2, duration - beatLength / SpaceBeat.Value);
                    break;

                // Decreases by requested spacing until (Time_between_notes - Spacing_requested < Shortest_duration_requested)
                // Afterward, takes duration = Shortest_duration_requested
                case 3:
                    if (duration > beatLength / ShortestBeat.Value + beatLength / SpaceBeat.Value)
                    {
                        duration -= beatLength / SpaceBeat.Value;
                    }
                    else if (duration / 2 >= beatLength / ShortestBeat.Value - beatLength / float_error_leniency)
                    {
                        duration = beatLength / ShortestBeat.Value;
                    }
                    else
                    {
                        duration = 0;
                    }

                    break;
            }

            return duration;
        }
    }
}
