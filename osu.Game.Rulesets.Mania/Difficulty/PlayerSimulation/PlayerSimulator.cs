// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

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
        private readonly ManiaBeatmap maniaBeatmap;

        private readonly List<ManiaHitObject> listObjects;
        private readonly int numberObjects;
        private readonly double releaseLeniency = 50;

        private readonly List<int> nextInColumn;
        private readonly List<int> previousInColumn;

        public readonly List<HitResult> Judgements = new List<HitResult>() { HitResult.Perfect, HitResult.Great, HitResult.Good, HitResult.Ok, HitResult.Meh, HitResult.Miss };

        public readonly List<double> PressTimings = new List<double>();
        public readonly List<double> ReleaseTimings = new List<double>();

        public void ScoreReset()
        {
            base.Reset(false);
        }

        public PlayerSimulator(IBeatmap beatmap)
        {
            maniaBeatmap = new ManiaBeatmap(new StageDefinition((int)beatmap.BeatmapInfo.Difficulty.CircleSize))
            {
                BeatmapInfo = beatmap.BeatmapInfo,
                HitObjects = (List<ManiaHitObject>)beatmap.HitObjects
            };
            listObjects = maniaBeatmap.HitObjects.OrderBy(h => h.StartTime).ToList();
            numberObjects = listObjects.Count;
            List<int> bufferedPreviousInColumn = Enumerable.Repeat(-1, maniaBeatmap.TotalColumns).ToList();

            nextInColumn = Enumerable.Repeat(-1, numberObjects).ToList();
            previousInColumn = Enumerable.Repeat(-1, numberObjects).ToList();
            PressTimings = Enumerable.Repeat(-1.0, numberObjects).ToList();
            ReleaseTimings = Enumerable.Repeat(-1.0, numberObjects).ToList();

            for (int i = 0; i < listObjects.Count; i++)
            {
                int column = listObjects[i].Column;
                if (bufferedPreviousInColumn[column] < 0)
                {
                    bufferedPreviousInColumn[column] = i;
                }
                else
                {
                    previousInColumn[i] = bufferedPreviousInColumn[column];
                    nextInColumn[bufferedPreviousInColumn[column]] = i;
                    bufferedPreviousInColumn[column] = i;
                }
            }
            foreach (int i in bufferedPreviousInColumn)
            {
                nextInColumn[i] = numberObjects;
            }
        }

        public void SimulatePlayer(double PlayerLevel)
        {
            List<int> currentHoldNoteId = new List<int>();
            List<HoldNote> currentHoldNote = new List<HoldNote>();

            for (int i = 0; i < numberObjects; i++)
            {
                var obj = listObjects[i];

                if (obj is HoldNote objHold)
                {
                    obj = objHold.Head;
                    currentHoldNoteId.Add(i);
                    currentHoldNote.Add(objHold);
                }

                var judgement = obj.CreateJudgement();

                for (int j = currentHoldNoteId.Count - 1; j >= 0; j--)
                {
                    HoldNote holdNote = currentHoldNote[j];
                    if (holdNote.EndTime <= obj.StartTime)
                    {
                        var judgementTail = holdNote.Tail.CreateJudgement();
                        var resultTail = CreateResult(holdNote, judgementTail) ?? throw new InvalidOperationException($"{GetType().ReadableName()} must provide a {nameof(JudgementResult)} through {nameof(CreateResult)}.");

                        resultTail.Type = GetSimulatedTailHitResult(holdNote.Tail, currentHoldNoteId[j], i, PlayerLevel, currentHoldNoteId);
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

                result.Type = GetSimulatedNoteHitResult(obj, i, PlayerLevel, currentHoldNoteId);

                ApplyResult(result);
            }

            for (int j = currentHoldNoteId.Count - 1; j >= 0; j--)
            {
                HoldNote holdNote = currentHoldNote[j];

                var judgementTail = holdNote.Tail.CreateJudgement();
                var resultTail = CreateResult(holdNote, judgementTail) ?? throw new InvalidOperationException($"{GetType().ReadableName()} must provide a {nameof(JudgementResult)} through {nameof(CreateResult)}.");
                resultTail.Type = GetSimulatedTailHitResult(holdNote.Tail, currentHoldNoteId[j], numberObjects, PlayerLevel, currentHoldNoteId);
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

        protected virtual HitResult GetSimulatedNoteHitResult(ManiaHitObject obj, int i, double PlayerLevel, List<int> currentHoldNoteId)
        {
            HitObject previousObj = null;
            double releaseLeniencyMultiplier = 0;
            if (previousInColumn[i] >= 0)
                previousObj = listObjects[previousInColumn[i]];
            if (previousObj is HoldNote holdNote)
            {
                previousObj = holdNote.Tail;
                releaseLeniencyMultiplier = 1;
            }
            int j = -1;
            double difficultyNote = double.PositiveInfinity;
            HitResult hitResultTried = HitResult.None;

            while (PlayerLevel < difficultyNote && j < 4)
            {
                j++;
                hitResultTried = Judgements[j];
                double hitWindow = obj.HitWindows.WindowFor(hitResultTried);

                if (previousInColumn[i] < 0)
                {
                    difficultyNote = 0;
                }
                else if (nextInColumn[i] >= numberObjects)
                {
                    double hitWindowPrevious = previousObj.HitWindows.WindowFor(hitResultTried);
                    double latestHitPossible = obj.StartTime - previousObj.StartTime + hitWindow + hitWindowPrevious + releaseLeniencyMultiplier * releaseLeniency;
                    difficultyNote = 1000.0 / latestHitPossible;
                }
                else
                {
                    double hitWindowPrevious = previousObj.HitWindows.WindowFor(hitResultTried);
                    double latestHitPossible = obj.StartTime - previousObj.StartTime + hitWindow + hitWindowPrevious + releaseLeniencyMultiplier * releaseLeniency;
                    double balancedTiming = (listObjects[nextInColumn[i]].StartTime - previousObj.StartTime + releaseLeniencyMultiplier * releaseLeniency) / 2;
                    difficultyNote = Math.Max(1000.0 / latestHitPossible, 1000.0 / balancedTiming);
                }

            };

            if (PlayerLevel < difficultyNote)
            {
                return Judgements[5];
            }
            PressTimings[i] = obj.HitWindows.WindowFor(hitResultTried) + obj.StartTime - 0.5;
            ReleaseTimings[i] = obj.HitWindows.WindowFor(hitResultTried) + obj.StartTime + 1 - 0.5;
            return hitResultTried;
        }
        protected virtual HitResult GetSimulatedTailHitResult(ManiaHitObject obj, int tailNote, int currentNote, double PlayerLevel, List<int> currentHoldNoteId)
        {
            int j = -1;
            double difficultyNote = double.PositiveInfinity;
            HitResult hitResultTried = HitResult.None;
            while (PlayerLevel < difficultyNote && j < 4)
            {
                j++;
                hitResultTried = Judgements[j];
                double hitWindow = obj.HitWindows.WindowFor(hitResultTried);

                if (nextInColumn[tailNote] >= numberObjects)
                {
                    difficultyNote = 0;
                }
                else
                {
                    double earliestReleasePossible = listObjects[nextInColumn[tailNote]].StartTime - obj.GetEndTime() + hitWindow + releaseLeniency;
                    difficultyNote = 1000.0 / earliestReleasePossible;
                }
            }
            if (PlayerLevel < difficultyNote)
            {
                return Judgements[5];
            }
            ReleaseTimings[tailNote] = -obj.HitWindows.WindowFor(hitResultTried) + obj.GetEndTime() + 0.5;
            return hitResultTried;
        }
    }
}
