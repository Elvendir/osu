// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Mania.Tests.Mods
{
    public partial class TestSceneManiaModInvert : ModTestScene
    {
        protected override Ruleset CreatePlayerRuleset() => new ManiaRuleset();

        [TestCase(4, 4, 1, 1)]
        [TestCase(1, 1, 1, 1)]
        [TestCase(8, 16, 1, 1)]
        [TestCase(4, 8, 2, 1)]
        [TestCase(4, 8, 3, 1)]
        [TestCase(4, 8, 0, 1)]
        public void VisualTestInversion(int spacing, int release, int holdtype, int shorttype) => CreateModTest(new ModTestData
        {
            Beatmap = createRawBeatmap(),
            Mod = new ManiaModInvert
            {
                SpaceBeat = { Value = spacing },
                ShortestBeat = { Value = release },
                TypeHoldNoteConversion = { Value = holdtype },
                TypeShortHoldNoteConversion = { Value = shorttype }
            },
            PassCondition = () => true
        });

        [TestCase(4, 4, 1, 1, 0)]
        [TestCase(1, 1, 1, 1, 1)]
        [TestCase(8, 16, 1, 1, 2)]
        [TestCase(4, 8, 2, 1, 3)]
        [TestCase(4, 8, 3, 1, 4)]
        [TestCase(4, 8, 0, 1, 5)]
        public void TestInversion(int spacing, int release, int holdtype, int shorttype, int testnumb)
        {
            var testBeatmap = createModdedBeatmap(spacing, release, holdtype, shorttype);
            int[] expectedHoldNotes = { 4, 2, 5, 4, 3, 3 };
            int[] expectedNotes = { 7, 7, 7, 7, 7, 6 };
            int count = testBeatmap.HitObjects.OfType<HoldNote>().ToList().Count;
            int count2 = testBeatmap.HitObjects.ToList().Count;
            Assert.That(count == expectedHoldNotes[testnumb] && count2 == expectedNotes[testnumb]);
        }

        private static ManiaBeatmap createRawBeatmap()
        {
            var beatmap = new ManiaBeatmap(new StageDefinition(1), 1);
            beatmap.ControlPointInfo.Add(0.0, new TimingControlPoint { BeatLength = 1000 }); // Set BPM to 60

            // Add test hit objects
            beatmap.HitObjects.Add(new Note { StartTime = 0 });
            beatmap.HitObjects.Add(new Note { StartTime = 63 }); // 1/16
            beatmap.HitObjects.Add(new Note { StartTime = 125 + 63 }); // 1/8
            beatmap.HitObjects.Add(new Note { StartTime = 250 + 125 + 63 }); // 1/4
            beatmap.HitObjects.Add(new Note { StartTime = 500 + 250 + 125 + 63 }); // 1/2
            beatmap.HitObjects.Add(new Note { StartTime = 1000 + 500 + 250 + 125 + 63 }); // 1
            beatmap.HitObjects.Add(new HoldNote { StartTime = 3000 + 500 + 250 + 125 + 63, EndTime = 250 + 3000 + 500 + 250 + 125 + 63 }); // 2 + 1/4 HoldNote
            return beatmap;
        }

        private static ManiaBeatmap createModdedBeatmap(int spacing, int release, int holdtype, int shorttype)
        {
            var beatmap = createRawBeatmap();
            var holdOffMod = new ManiaModInvert
            {
                SpaceBeat = { Value = spacing },
                ShortestBeat = { Value = release },
                TypeHoldNoteConversion = { Value = holdtype },
                TypeShortHoldNoteConversion = { Value = shorttype }
            };

            foreach (var hitObject in beatmap.HitObjects)
                hitObject.ApplyDefaults(beatmap.ControlPointInfo, new BeatmapDifficulty());

            holdOffMod.ApplyToBeatmap(beatmap);

            return beatmap;
        }
    }
}
