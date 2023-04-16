// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Extensions.TypeExtensions;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.Difficulty.PlayerSimulation
{
    internal partial class PlayerSimulator : ManiaScoreProcessor
    {
        public readonly List<HitResult> Judgements = new List<HitResult>() { HitResult.Perfect, HitResult.Great, HitResult.Good, HitResult.Ok, HitResult.Meh, HitResult.Miss };
        public void SimulatePlayer(IBeatmap beatmap, double PlayerLevel)
        {
            var maniaBeatmap = new ManiaBeatmap(new StageDefinition((int)beatmap.BeatmapInfo.Difficulty.CircleSize))
            {
                BeatmapInfo = beatmap.BeatmapInfo,
                HitObjects = (List<ManiaHitObject>)beatmap.HitObjects
            };

            foreach (var column in maniaBeatmap.HitObjects.GroupBy(h => h.Column))
            {
                var listObjects = column.ToList();
                for (int i = 0; i < listObjects.Count; i++)
                {
                    var obj = listObjects[i];
                    var judgement = obj.CreateJudgement();

                    if (obj is HoldNote objHold)
                    {
                        judgement = objHold.Head.CreateJudgement();
                    }

                    var result = CreateResult(obj, judgement) ?? throw new InvalidOperationException($"{GetType().ReadableName()} must provide a {nameof(JudgementResult)} through {nameof(CreateResult)}.");

                    result.Type = GetSimulatedPlayerHitResult(judgement, listObjects, i, PlayerLevel);

                    ApplyResult(result);
                }
            }
        }

        protected virtual HitResult GetSimulatedPlayerHitResult(Judgement judgement, List<ManiaHitObject> listObjects, int i, double PlayerLevel)
        {
            int j = 0;
            List<double> difficultyPerJudgement = DifficultyPerJudgement(listObjects, i);
            while (PlayerLevel < difficultyPerJudgement[j] && j < 5) j++;
            return Judgements[j];
        }


        protected virtual List<double> DifficultyPerJudgement(List<ManiaHitObject> listObjects, int i)
        {
            if (i == 0)
            {
                return new List<double>() { 0, 0, 0, 0, 0, 0 };
            }
            else if (i == listObjects.Count - 1)
            {
                List<double> difficultyPerJudgement = new List<double> { 0, 0, 0, 0, 0, 0 };
                for (int j = 0; j < difficultyPerJudgement.Count - 1; j++)
                {
                    double latestHitPossible = listObjects[i].StartTime - listObjects[i - 1].StartTime + listObjects[i].HitWindows.WindowFor(Judgements[j]);
                    difficultyPerJudgement[j] = 1000.0 / latestHitPossible;
                }
                return difficultyPerJudgement;

            }
            else
            {
                List<double> difficultyPerJudgement = new List<double> { 0, 0, 0, 0, 0, 0 };
                for (int j = 0; j < difficultyPerJudgement.Count - 1; j++)
                {
                    double latestHitPossible = listObjects[i].StartTime - listObjects[i - 1].StartTime + listObjects[i].HitWindows.WindowFor(Judgements[j]);
                    double balancedTiming = (listObjects[i + 1].StartTime - listObjects[i - 1].StartTime) / 2;
                    difficultyPerJudgement[j] = Math.Max(1000.0 / latestHitPossible, 1000.0 / balancedTiming);
                }
                return difficultyPerJudgement;
            }
        }
    }
}
