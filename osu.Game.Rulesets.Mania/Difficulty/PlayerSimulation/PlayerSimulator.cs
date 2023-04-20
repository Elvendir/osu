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
        public readonly List<HitResult> Judgements = new List<HitResult>() { HitResult.Perfect, HitResult.Great, HitResult.Good, HitResult.Ok, HitResult.Meh, HitResult.Miss };
        public readonly List<int> HitResults = Enumerable.Repeat(0, 6).ToList();

        private readonly List<ManiaHitObject> listObjects;
        public readonly List<int> StartNoteIds;
        public readonly List<ManiaHitObject> ListNestedObjects = new List<ManiaHitObject>();

        private readonly int numberObjects;
        public readonly int NumberNestedObjects;
        private readonly double releaseLeniency = 50;

        public readonly List<int> NextInColumn;
        public readonly List<int> PreviousInColumn;

        private readonly List<int> nextInColumnIgnoringMiss;
        private readonly List<int> previousInColumnIgnoringMiss;

        public readonly List<double> PressTimings = new List<double>();

        public void ScoreReset()
        {
            base.Reset(false);
            for (int i = 0; i < NumberNestedObjects; i++)
            {
                PressTimings[i] = double.NegativeInfinity;
                previousInColumnIgnoringMiss[i] = PreviousInColumn[i];
                nextInColumnIgnoringMiss[i] = NextInColumn[i];
            }
            for (int i = 0; i < 6; i++)
            {
                HitResults[i] = 0;
            }
        }

        public PlayerSimulator(IBeatmap beatmap)
        {
            ApplyBeatmap(beatmap);
            maniaBeatmap = new ManiaBeatmap(new StageDefinition((int)beatmap.BeatmapInfo.Difficulty.CircleSize))
            {
                BeatmapInfo = beatmap.BeatmapInfo,
                HitObjects = (List<ManiaHitObject>)beatmap.HitObjects
            };

            listObjects = maniaBeatmap.HitObjects.OrderBy(h => h.StartTime).ToList();
            numberObjects = listObjects.Count;
            StartNoteIds = Enumerable.Repeat(-1, numberObjects).ToList();

            foreach (ManiaHitObject hitObject in listObjects)
            {
                if (hitObject is HoldNote holdObj)
                {
                    foreach (ManiaHitObject nestedObject in holdObj.NestedHitObjects)
                    {
                        ListNestedObjects.Add(nestedObject);
                    }
                }
                else
                {
                    ListNestedObjects.Add(hitObject);
                }

            }
            ListNestedObjects = ListNestedObjects.OrderBy(h => h.StartTime).ToList();
            NumberNestedObjects = ListNestedObjects.Count;

            List<int> bufferedPreviousInColumn = Enumerable.Repeat(-1, maniaBeatmap.TotalColumns).ToList();

            NextInColumn = Enumerable.Repeat(-1, NumberNestedObjects).ToList();
            PreviousInColumn = Enumerable.Repeat(-1, NumberNestedObjects).ToList();
            nextInColumnIgnoringMiss = Enumerable.Repeat(-1, NumberNestedObjects).ToList();
            previousInColumnIgnoringMiss = Enumerable.Repeat(-1, NumberNestedObjects).ToList();

            PressTimings = Enumerable.Repeat(double.NegativeInfinity, NumberNestedObjects).ToList();

            int j = 0;
            for (int i = 0; i < NumberNestedObjects; i++)
            {
                int column = ListNestedObjects[i].Column;
                if (bufferedPreviousInColumn[column] < 0)
                {
                    bufferedPreviousInColumn[column] = i;
                }
                else
                {
                    PreviousInColumn[i] = bufferedPreviousInColumn[column];
                    previousInColumnIgnoringMiss[i] = bufferedPreviousInColumn[column];
                    NextInColumn[bufferedPreviousInColumn[column]] = i;
                    nextInColumnIgnoringMiss[bufferedPreviousInColumn[column]] = i;
                    if (ListNestedObjects[i] is not HoldNoteTick)
                        bufferedPreviousInColumn[column] = i; ;
                }
                if (ListNestedObjects[i] is not TailNote && ListNestedObjects[i] is not HoldNoteTick)
                {
                    StartNoteIds[j] = i;
                    j++;
                }
            }
            foreach (int i in bufferedPreviousInColumn)
            {
                if (i >= 0)
                {
                    NextInColumn[i] = NumberNestedObjects;
                    nextInColumnIgnoringMiss[i] = NumberNestedObjects;
                }
            }

            bufferedPreviousInColumn = Enumerable.Repeat(-1, maniaBeatmap.TotalColumns).ToList();
            for (int i = NumberNestedObjects - 1; i >= 0; i--)
            {
                int column = ListNestedObjects[i].Column;

                if (NextInColumn[i] <= 0)
                {
                    NextInColumn[i] = bufferedPreviousInColumn[column];
                    nextInColumnIgnoringMiss[i] = bufferedPreviousInColumn[column];
                }
                else
                {
                    bufferedPreviousInColumn[column] = i;
                }
            }
        }

        public void SimulatePlayer(double PlayerLevel)
        {

            SimulateHitTimings(PlayerLevel);
            int a = PressTimings.Where(x => x < 0).Count();

            for (int i = 0; i < NumberNestedObjects; i++)
            {
                var obj = ListNestedObjects[i];
                if (obj is HoldNoteTick objTick)
                {
                    var judgement = objTick.CreateJudgement();
                    var result = CreateResult(objTick, judgement) ?? throw new InvalidOperationException($"{GetType().ReadableName()} must provide a {nameof(JudgementResult)} through {nameof(CreateResult)}.");
                    if (PressTimings[i] > double.NegativeInfinity)
                    {
                        result.Type = HitResult.LargeTickHit;
                    }
                    else
                    {
                        result.Type = HitResult.LargeTickMiss;
                    }
                    ApplyResult(result);
                }
                else
                {
                    var judgement = obj.CreateJudgement();
                    var result = CreateResult(obj, judgement) ?? throw new InvalidOperationException($"{GetType().ReadableName()} must provide a {nameof(JudgementResult)} through {nameof(CreateResult)}.");
                    result.Type = GetJudgementFromTiming(PressTimings[i], obj.StartTime, obj.HitWindows);
                    if (obj is TailNote && NextInColumn[i] < NumberNestedObjects && PressTimings[i] >= ListNestedObjects[NextInColumn[i]].StartTime)
                    {
                        result.Type = HitResult.Miss;
                    }
                    HitResults[result.Type.GetIndexForOrderedDisplay()] += 1;
                    ApplyResult(result);
                }
            }
        }

        protected void SimulateHitTimings(double PlayerLevel)
        {
            List<int> currentHoldNoteTailId = new List<int>();

            for (int i = 0; i < NumberNestedObjects; i++)
            {
                var obj = ListNestedObjects[i];

                if (obj is HoldNoteTick)
                {
                    if (PressTimings[PreviousInColumn[i]] > double.NegativeInfinity)
                        PressTimings[i] = 1;
                }
                else
                {
                    if (obj is TailNote)
                    {
                        GetSimulatedReleaseTiming(i, PlayerLevel, currentHoldNoteTailId);
                        currentHoldNoteTailId.Remove(i);
                    }
                    else
                    {
                        GetSimulatedHitTiming(i, PlayerLevel, currentHoldNoteTailId);

                    }

                    if (!(PressTimings[i] > double.NegativeInfinity))
                    {
                        if (nextInColumnIgnoringMiss[i] < NumberNestedObjects)
                            previousInColumnIgnoringMiss[NextInColumn[i]] = previousInColumnIgnoringMiss[i];
                        if (PreviousInColumn[i] >= 0)
                            nextInColumnIgnoringMiss[previousInColumnIgnoringMiss[i]] = NextInColumn[i];
                    }
                    else
                    {
                        if (obj is HeadNote)
                            currentHoldNoteTailId.Add(nextInColumnIgnoringMiss[i]);
                    }
                }
            }
        }

        protected void GetSimulatedHitTiming(int currentObjId, double PlayerLevel, List<int> currentHoldNoteId)
        {
            HitObject currentObj = ListNestedObjects[currentObjId];
            int previousNotMissedObjId = previousInColumnIgnoringMiss[currentObjId];
            int previousObjId = PreviousInColumn[currentObjId];
            double perfectHitWindow = currentObj.HitWindows.WindowFor(HitResult.Perfect) - 1;

            if (previousNotMissedObjId < 0)
            {
                PressTimings[currentObjId] = currentObj.StartTime - perfectHitWindow;
            }
            else
            {
                HitObject previousNotMissedObj = ListNestedObjects[previousNotMissedObjId];
                HitObject previousObj = ListNestedObjects[previousObjId];
                double releaseLeniencyMultiplier = 0;
                double lastObjectTime = PressTimings[previousNotMissedObjId];
                double previousObjectHitableLatestTime = 0;

                int nextPressObjId = NextInColumn[currentObjId];

                if (previousNotMissedObjId != previousObjId)
                {
                    previousObjectHitableLatestTime = ListNestedObjects[previousObjId].StartTime + ListNestedObjects[previousObjId].MaximumJudgementOffset + 1;
                }
                else if (previousObj is TailNote)
                {
                    previousObjectHitableLatestTime = ListNestedObjects[previousObjId].StartTime + 1;
                }

                if (previousNotMissedObj is TailNote)
                {
                    releaseLeniencyMultiplier = 1;
                    //if (PressTimings[previousNotMissedObjId] > currentObj.StartTime)
                    //    return;
                }

                double tryHitAt = 1000.0 / PlayerLevel + lastObjectTime - releaseLeniencyMultiplier * releaseLeniency;
                tryHitAt = Math.Max(tryHitAt, currentObj.StartTime - perfectHitWindow);

                if (tryHitAt < currentObj.StartTime)
                {
                    PressTimings[currentObjId] = Math.Min(currentObj.StartTime, Math.Max(tryHitAt, previousObjectHitableLatestTime));
                }
                else if (tryHitAt < currentObj.StartTime + currentObj.HitWindows.WindowFor(HitResult.Meh) && (nextPressObjId >= NumberNestedObjects || tryHitAt < ListNestedObjects[nextPressObjId].StartTime))
                {
                    PressTimings[currentObjId] = tryHitAt;
                }
            }
        }
        protected void GetSimulatedReleaseTiming(int currentObjId, double PlayerLevel, List<int> currentHoldNoteId)
        {
            TailNote currentObj = (TailNote)ListNestedObjects[currentObjId];
            int previousObjectId = PreviousInColumn[currentObjId];
            //   double perfectReleaseWindow = currentObj.HitWindows.WindowFor(HitResult.Perfect) - 1;
            //   double tryReleaseAt = 500.0 / PlayerLevel + PressTimings[previousObjectId] - releaseLeniency;

            if (PressTimings[previousObjectId] > double.NegativeInfinity)
            {
                //if (tryReleaseAt < currentObj.StartTime - perfectReleaseWindow)
                //{
                //    PressTimings[currentObjId] = Math.Max(currentObj.StartTime - perfectReleaseWindow, PressTimings[previousObjectId] + 1);
                //}
                //else
                //{
                PressTimings[currentObjId] = Math.Max(currentObj.StartTime, PressTimings[previousObjectId] + 1);
                //}
            }
        }

        protected HitResult GetJudgementFromTiming(double pressTiming, double startTime, HitWindows hitWindows)
        {
            if (pressTiming > double.NegativeInfinity)
            {
                return hitWindows.ResultFor(pressTiming - startTime);
            }
            else
            {
                return HitResult.Miss;
            }
        }
    }
}
