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
using osu.Game.Rulesets.Objects;

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

            var listObjects = maniaBeatmap.HitObjects.OrderBy(h => h.StartTime).ToList();

            List<int> previousInColumn = Enumerable.Repeat(-1, maniaBeatmap.TotalColumns).ToList();
            List<int> nextInColumn = Enumerable.Repeat(-1, maniaBeatmap.TotalColumns).ToList();
            List<int> currentHoldNoteId = new List<int>();
            List<HoldNote> currentHoldNote = new List<HoldNote>();

            for (int i = 0; i < maniaBeatmap.TotalColumns; i++)
            {
                nextInColumn[i] = getNextInColumn(listObjects, 0);
            }

            for (int i = 0; i < listObjects.Count; i++)
            {
                var obj = listObjects[i];
                var judgement = obj.CreateJudgement();
                nextInColumn[obj.Column] = getNextInColumn(listObjects, i);

                if (obj is HoldNote objHold)
                {
                    judgement = objHold.Head.CreateJudgement();
                    currentHoldNoteId.Add(i);
                    currentHoldNote.Add(objHold);
                }

                for (int j = currentHoldNoteId.Count - 1; j >= 0; j--)
                {
                    HoldNote holdNote = currentHoldNote[j];
                    if (holdNote.EndTime <= obj.StartTime)
                    {
                        var judgementTail = holdNote.Tail.CreateJudgement();
                        var resultTail = CreateResult(holdNote, judgementTail) ?? throw new InvalidOperationException($"{GetType().ReadableName()} must provide a {nameof(JudgementResult)} through {nameof(CreateResult)}.");
                        resultTail.Type = GetSimulatedTailHitResult(judgementTail, listObjects, j, PlayerLevel, previousInColumn, nextInColumn, currentHoldNoteId);
                        ApplyResult(resultTail);

                        foreach (HoldNoteTick tick in holdNote.NestedHitObjects.OfType<HoldNoteTick>())
                        {
                            var judgementTick = tick.CreateJudgement();
                            var resultTick = CreateResult(holdNote, judgementTick) ?? throw new InvalidOperationException($"{GetType().ReadableName()} must provide a {nameof(JudgementResult)} through {nameof(CreateResult)}.");
                            resultTick.Type = HitResult.LargeTickHit;
                            ApplyResult(resultTick);
                        }

                        currentHoldNote.RemoveAt(j);
                        currentHoldNoteId.RemoveAt(j);
                    }
                }

                var result = CreateResult(obj, judgement) ?? throw new InvalidOperationException($"{GetType().ReadableName()} must provide a {nameof(JudgementResult)} through {nameof(CreateResult)}.");

                result.Type = GetSimulatedNoteHitResult(judgement, listObjects, i, PlayerLevel, previousInColumn, nextInColumn, currentHoldNoteId);

                ApplyResult(result);
                previousInColumn[obj.Column] = i;
            }

            for (int j = currentHoldNoteId.Count - 1; j >= 0; j--)
            {
                HoldNote holdNote = currentHoldNote[j];

                var judgementTail = holdNote.Tail.CreateJudgement();
                var resultTail = CreateResult(holdNote, judgementTail) ?? throw new InvalidOperationException($"{GetType().ReadableName()} must provide a {nameof(JudgementResult)} through {nameof(CreateResult)}.");
                resultTail.Type = GetSimulatedTailHitResult(judgementTail, listObjects, j, PlayerLevel, previousInColumn, nextInColumn, currentHoldNoteId);
                ApplyResult(resultTail);

                foreach (HoldNoteTick tick in holdNote.NestedHitObjects.OfType<HoldNoteTick>())
                {
                    var judgementTick = tick.CreateJudgement();
                    var resultTick = CreateResult(holdNote, judgementTick) ?? throw new InvalidOperationException($"{GetType().ReadableName()} must provide a {nameof(JudgementResult)} through {nameof(CreateResult)}.");
                    resultTick.Type = HitResult.LargeTickHit;
                    ApplyResult(resultTick);
                }

                currentHoldNote.RemoveAt(j);
                currentHoldNoteId.RemoveAt(j);
            }
        }

        protected virtual HitResult GetSimulatedNoteHitResult(Judgement judgement, List<ManiaHitObject> listObjects, int i, double PlayerLevel, List<int> previousInColumn, List<int> nextInColumn, List<int> currentHoldNoteId)
        {
            var obj = listObjects[i];
            int j = -1;
            double difficultyNote = double.PositiveInfinity;
            HitResult hitResultTried = HitResult.None;
            while (PlayerLevel <= difficultyNote && j < 4)
            {
                j++;
                hitResultTried = Judgements[j];

                if (previousInColumn[obj.Column] < 0)
                {
                    difficultyNote = 0;
                }
                else if (nextInColumn[obj.Column] >= listObjects.Count)
                {
                    double latestHitPossible = obj.StartTime - listObjects[previousInColumn[obj.Column]].StartTime + listObjects[i].HitWindows.WindowFor(Judgements[j]);
                    difficultyNote = 1000.0 / latestHitPossible;
                }
                else
                {
                    double latestHitPossible = obj.StartTime - listObjects[previousInColumn[obj.Column]].StartTime + listObjects[i].HitWindows.WindowFor(Judgements[j]);
                    double balancedTiming = (listObjects[nextInColumn[obj.Column]].StartTime - listObjects[previousInColumn[obj.Column]].GetEndTime()) / 2;
                    difficultyNote = Math.Max(1000.0 / latestHitPossible, 1000.0 / balancedTiming);
                }

            };

            if (PlayerLevel <= difficultyNote)
            {
                return Judgements[5];
            }

            return hitResultTried;
        }
        protected virtual HitResult GetSimulatedTailHitResult(Judgement judgement, List<ManiaHitObject> listObjects, int i, double PlayerLevel, List<int> previousInColumn, List<int> nextInColumn, List<int> currentHoldNoteId)
        {
            return Judgements[0];
        }

        private int getNextInColumn(List<ManiaHitObject> listObjects, int i)
        {
            int j = i + 1;
            while (j < listObjects.Count() && listObjects[j].Column != listObjects[i].Column) j++;
            return j;
        }
    }
}
