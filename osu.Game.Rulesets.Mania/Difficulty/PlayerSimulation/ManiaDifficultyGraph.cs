// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.PlayerSimulation;
using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays.Settings;
using osuTK.Graphics;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osuTK;
using osuTK.Input;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Scoring;
using osu.Framework.Extensions;
using System.Diagnostics;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.Difficulty.PlayerSimulation
{
    public partial class ManiaDifficultyGraph : DifficultyGraph
    {
        private GraphContainer graphs = null!;
        private FillFlowContainer legend = null!;
        private int maxPlayerLevel = 10;

        private readonly List<HitResult> judgements = new List<HitResult>() { HitResult.Perfect, HitResult.Great, HitResult.Good, HitResult.Ok, HitResult.Meh, HitResult.Miss };

        public ManiaDifficultyGraph()
        {
            Children = new Drawable[]
            {
                new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    RowDimensions = new[]
                    {
                        new Dimension(),
                        new Dimension(GridSizeMode.AutoSize),
                    },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            graphs = new GraphContainer
                            {
                                RelativeSizeAxes = Axes.Both,
                            },
                        },
                        new Drawable[]
                        {
                            legend = new FillFlowContainer
                            {
                                Padding = new MarginPadding(5),
                                Direction = FillDirection.Full,
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                            },
                        },
                    }
                }
            };
        }

        private const int base_great = 300;
        private const int base_ok = 100;

        public override void UpdateDifficultyGraph(WorkingBeatmap workingBeatmap)
        {
            graphs.Clear();
            legend.Clear();
            runForProcessor("lazer-standardised", workingBeatmap.GetPlayableBeatmap(new ManiaRuleset().RulesetInfo), Color4.Blue);
        }

        private void runForProcessor(string name, IBeatmap beatmap, Color4 colour)
        {

            List<float> results = new List<float>();
            List<List<float>> judgement_percent = new List<List<float>>();
            float maxHitEvents = 1;

            for (double i = 0; i < maxPlayerLevel; i += 0.1)
            {
                var processor = new PlayerSimulator();
                processor.ApplyBeatmap(beatmap);
                processor.SimulatePlayer(beatmap, i);
                results.Add(processor.TotalScore.Value);
                if (i == 0)
                    for (int j = 0; j < 6; j++)
                        judgement_percent.Add(new List<float>());
                for (int j = 0; j < 6; j++)
                {
                    float judgment_percent_f = processor.HitEvents.Where(d => d.Result == processor.Judgements[j]).Count();
                    maxHitEvents = processor.HitEvents.Where(d => processor.Judgements.Contains(d.Result)).Count();
                    if (j == 0)
                    {
                        judgement_percent[j].Add(judgment_percent_f);
                    }
                    else
                    {
                        judgement_percent[j].Add(judgment_percent_f + judgement_percent[j - 1].Last());
                    }
                }

            }
            List<Color4> colors = new List<Color4>() { Color4.White, Color4.Yellow, Color4.Green, Color4.Cyan, Color4.Red };
            graphs.MaxCombo.Value = maxPlayerLevel;

            graphs.Add(new LineGraph
            {
                Name = name,
                RelativeSizeAxes = Axes.Both,
                LineColour = colour,
                MaxValue = 1000000,
                MinValue = 0,
                Values = results,
            });
            for (int j = 0; j < 5; j++)
            {
                graphs.Add(new LineGraph
                {
                    Name = judgements[j].ToString(),
                    RelativeSizeAxes = Axes.Both,
                    MaxValue = maxHitEvents,
                    MinValue = 0,
                    LineColour = colors[j],
                    Values = judgement_percent[j],
                });
            }

            legend.Add(new OsuSpriteText
            {
                Colour = colour,
                RelativeSizeAxes = Axes.X,
                Width = 0.5f,
                Text = $"{FontAwesome.Solid.Circle.Icon} {name}"
            });

            legend.Add(new OsuSpriteText
            {
                Colour = colour,
                RelativeSizeAxes = Axes.X,
                Width = 0.5f,
                Text = $"Score {results.Last():#,0} at PlayerLevel {maxPlayerLevel:#,0}"
            });
        }

    }

    public partial class GraphContainer : Container, IHasCustomTooltip<IEnumerable<LineGraph>>
    {
        public readonly BindableList<double> MissLocations = new BindableList<double>();
        public readonly BindableList<double> NonPerfectLocations = new BindableList<double>();

        public Bindable<int> MaxCombo = new Bindable<int>();

        protected override Container<Drawable> Content { get; } = new Container { RelativeSizeAxes = Axes.Both };

        private readonly Box hoverLine;

        private readonly Container missLines;
        private readonly Container verticalGridLines;

        public int CurrentHoverCombo { get; private set; }

        public GraphContainer()
        {
            InternalChild = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    new Box
                    {
                        Colour = OsuColour.Gray(0.1f),
                        RelativeSizeAxes = Axes.Both,
                    },
                    verticalGridLines = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                    hoverLine = new Box
                    {
                        Colour = Color4.Yellow,
                        RelativeSizeAxes = Axes.Y,
                        Origin = Anchor.TopCentre,
                        Alpha = 0,
                        Width = 1,
                    },
                    missLines = new Container
                    {
                        Alpha = 0.6f,
                        RelativeSizeAxes = Axes.Both,
                    },
                    Content,
                }
            };

            MissLocations.BindCollectionChanged((_, _) => updateMissLocations());
            NonPerfectLocations.BindCollectionChanged((_, _) => updateMissLocations());

            MaxCombo.BindValueChanged(_ =>
            {
                updateMissLocations();
                updateVerticalGridLines();
            }, true);
        }

        private void updateVerticalGridLines()
        {
            verticalGridLines.Clear();

            for (int i = 0; i < MaxCombo.Value; i++)
            {
                if (i % 1 == 0)
                {
                    verticalGridLines.AddRange(new Drawable[]
                    {
                        new Box
                        {
                            Colour = OsuColour.Gray(0.2f),
                            Origin = Anchor.TopCentre,
                            Width = 1,
                            RelativeSizeAxes = Axes.Y,
                            RelativePositionAxes = Axes.X,
                            X = (float)i / MaxCombo.Value,
                        },
                        new OsuSpriteText
                        {
                            RelativePositionAxes = Axes.X,
                            X = (float)i / MaxCombo.Value,
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomLeft,
                            Text = $"{i:#,0}",
                            Rotation = -30,
                            Y = -20,
                        }
                    });
                }
            }
        }

        private void updateMissLocations()
        {
            missLines.Clear();

            foreach (int miss in MissLocations)
            {
                missLines.Add(new Box
                {
                    Colour = Color4.Red,
                    Origin = Anchor.TopCentre,
                    Width = 1,
                    RelativeSizeAxes = Axes.Y,
                    RelativePositionAxes = Axes.X,
                    X = (float)miss / MaxCombo.Value,
                });
            }

            foreach (int miss in NonPerfectLocations)
            {
                missLines.Add(new Box
                {
                    Colour = Color4.Orange,
                    Origin = Anchor.TopCentre,
                    Width = 1,
                    RelativeSizeAxes = Axes.Y,
                    RelativePositionAxes = Axes.X,
                    X = (float)miss / MaxCombo.Value,
                });
            }
        }

        protected override bool OnHover(HoverEvent e)
        {
            hoverLine.Show();
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            hoverLine.Hide();
            base.OnHoverLost(e);
        }

        protected override bool OnMouseMove(MouseMoveEvent e)
        {
            CurrentHoverCombo = (int)(e.MousePosition.X / DrawWidth * MaxCombo.Value);

            hoverLine.X = e.MousePosition.X;
            return base.OnMouseMove(e);
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            if (e.Button == MouseButton.Left)
                MissLocations.Add(CurrentHoverCombo);
            else
                NonPerfectLocations.Add(CurrentHoverCombo);

            return true;
        }

        private GraphTooltip? tooltip;

        public ITooltip<IEnumerable<LineGraph>> GetCustomTooltip() => tooltip ??= new GraphTooltip(this);

        public IEnumerable<LineGraph> TooltipContent => Content.OfType<LineGraph>();

        public partial class GraphTooltip : CompositeDrawable, ITooltip<IEnumerable<LineGraph>>
        {
            private readonly GraphContainer graphContainer;

            private readonly OsuTextFlowContainer textFlow;

            public GraphTooltip(GraphContainer graphContainer)
            {
                this.graphContainer = graphContainer;
                AutoSizeAxes = Axes.Both;

                Masking = true;
                CornerRadius = 10;

                InternalChildren = new Drawable[]
                {
                    new Box
                    {
                        Colour = OsuColour.Gray(0.15f),
                        RelativeSizeAxes = Axes.Both,
                    },
                    textFlow = new OsuTextFlowContainer
                    {
                        Colour = Color4.White,
                        AutoSizeAxes = Axes.Both,
                        Padding = new MarginPadding(10),
                    }
                };
            }

            private int? lastContentCombo;
            public void SetContent(IEnumerable<LineGraph> content)
            {
                int relevantCombo = graphContainer.CurrentHoverCombo;

                if (lastContentCombo == relevantCombo)
                    return;

                lastContentCombo = relevantCombo;
                textFlow.Clear();

                textFlow.AddParagraph($"At PlayerLevel {relevantCombo}:");

                float lastHits = 0;
                foreach (var graph in content)
                {
                    float valueAtHover = graph.Values.ElementAt(relevantCombo * 10);
                    float ofTotal = valueAtHover / graph.Values.Last();

                    if (graph.Name == "lazer-standardised")
                    {
                        textFlow.AddParagraph($"{graph.Name}: {valueAtHover:#,0} ({ofTotal * 100:N0}% of final)\n", st => st.Colour = graph.LineColour);

                    }
                    else
                    {
                        textFlow.AddParagraph($"{graph.Name}: {valueAtHover - lastHits:#,0} ", st => st.Colour = graph.LineColour);
                        lastHits = valueAtHover;
                    }
                }
            }

            public void Move(Vector2 pos) => this.MoveTo(pos);
        }
    }
}
