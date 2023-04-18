﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Difficulty.PlayerSimulation;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Replays;

namespace osu.Game.Rulesets.Mania.Replays
{
    internal class ManiaPlayerGenerator : AutoGenerator<ManiaReplayFrame>
    {
        public const double RELEASE_DELAY = 20;

        public new ManiaBeatmap Beatmap => (ManiaBeatmap)base.Beatmap;
        public PlayerSimulator Simulator;

        private readonly ManiaAction[] columnActions;

        public ManiaPlayerGenerator(ManiaBeatmap beatmap, double PlayerLevel)
            : base(beatmap)
        {
            columnActions = new ManiaAction[Beatmap.TotalColumns];

            var normalAction = ManiaAction.Key1;
            var specialAction = ManiaAction.Special1;
            int totalCounter = 0;

            foreach (var stage in Beatmap.Stages)
            {
                for (int i = 0; i < stage.Columns; i++)
                {
                    if (stage.IsSpecialColumn(i))
                        columnActions[totalCounter] = specialAction++;
                    else
                        columnActions[totalCounter] = normalAction++;
                    totalCounter++;
                }
            }
            Simulator = new PlayerSimulator(Beatmap);
            Simulator.SimulatePlayer(PlayerLevel);
        }

        protected override void GenerateFrames()
        {
            if (Beatmap.HitObjects.Count == 0)
                return;

            var pointGroups = generateActionPoints().GroupBy(a => a.Time).OrderBy(g => g.First().Time);

            var actions = new List<ManiaAction>();

            foreach (var group in pointGroups)
            {
                foreach (var point in group)
                {
                    switch (point)
                    {
                        case HitPoint:
                            actions.Add(columnActions[point.Column]);
                            break;

                        case ReleasePoint:
                            actions.Remove(columnActions[point.Column]);
                            break;
                    }
                }

                Frames.Add(new ManiaReplayFrame(group.First().Time, actions.ToArray()));
            }
        }

        private IEnumerable<IActionPoint> generateActionPoints()
        {
            for (int i = 0; i < Beatmap.HitObjects.Count(); i++)
            {
                var currentObject = Beatmap.HitObjects.OrderBy(h => h.StartTime).ToList()[i];
                if (Simulator.PressTimings[i] >= 0)
                {
                    yield return new HitPoint { Time = Simulator.PressTimings[i], Column = currentObject.Column };

                    yield return new ReleasePoint { Time = Simulator.ReleaseTimings[i], Column = currentObject.Column };
                }
            }
        }

        private double calculateReleaseTime(HitObject currentObject, HitObject? nextObject)
        {
            double endTime = currentObject.GetEndTime();

            if (currentObject is HoldNote)
                // hold note releases must be timed exactly.
                return endTime;

            bool canDelayKeyUpFully = nextObject == null ||
                                      nextObject.StartTime > endTime + RELEASE_DELAY;

            return endTime + (canDelayKeyUpFully ? RELEASE_DELAY : (nextObject.AsNonNull().StartTime - endTime) * 0.9);
        }

        protected override HitObject? GetNextObject(int currentIndex)
        {
            int desiredColumn = Beatmap.HitObjects[currentIndex].Column;

            for (int i = currentIndex + 1; i < Beatmap.HitObjects.Count; i++)
            {
                if (Beatmap.HitObjects[i].Column == desiredColumn)
                    return Beatmap.HitObjects[i];
            }

            return null;
        }

        private interface IActionPoint
        {
            double Time { get; set; }
            int Column { get; set; }
        }

        private struct HitPoint : IActionPoint
        {
            public double Time { get; set; }
            public int Column { get; set; }
        }

        private struct ReleasePoint : IActionPoint
        {
            public double Time { get; set; }
            public int Column { get; set; }
        }
    }
}
