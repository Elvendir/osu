// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Allocation;
using osu.Game.Beatmaps;

namespace osu.Game.Rulesets.Difficulty.PlayerSimulation
{
    public partial class BeatmapDifficultyGraph : Container
    {

        [Resolved]
        private OsuGameBase game { get; set; }

        private IBindable<RulesetInfo> gameRuleset;
        private Ruleset ruleset;
        private WorkingBeatmap beatmap;

        public virtual WorkingBeatmap Beatmap
        {
            get => beatmap;
            set
            {
                beatmap = value;
                if (beatmap is not DummyWorkingBeatmap)
                {
                    if (difficultyGraph is null)
                    {
                        content.Clear();
                        ruleset = gameRuleset.Value.CreateInstance();
                        difficultyGraph = ruleset.CreateDifficultyGraph();
                        difficultyGraph.RelativeSizeAxes = Axes.Both;
                        difficultyGraph.UpdateDifficultyGraph(beatmap);
                        content.Add(difficultyGraph);
                    }
                    else
                    {
                        difficultyGraph.UpdateDifficultyGraph(beatmap);
                    }
                }
            }
        }

        private DifficultyGraph difficultyGraph;
        private readonly Container content;

        protected override void LoadComplete()
        {
            base.LoadComplete();
            gameRuleset = game.Ruleset.GetBoundCopy();
            gameRuleset.BindValueChanged(_ =>
            {
                content.Clear();
                ruleset = gameRuleset.Value.CreateInstance();
                difficultyGraph = ruleset.CreateDifficultyGraph();
                difficultyGraph.RelativeSizeAxes = Axes.Both;
                if (beatmap is not DummyWorkingBeatmap)
                {
                    difficultyGraph.UpdateDifficultyGraph(beatmap);
                }
                content.Add(difficultyGraph);
            });
        }
        public BeatmapDifficultyGraph()
        {
            Children = new Drawable[]
            {
                //new Box
                //{
                //    RelativeSizeAxes = Axes.Both,
                //    Colour = Color4.Black,

                //},
                content = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                },
            };
        }
    }
}


