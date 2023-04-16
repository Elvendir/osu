// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps;

namespace osu.Game.Rulesets.Difficulty.PlayerSimulation
{
    public abstract partial class DifficultyGraph : Container
    {
        public abstract void UpdateDifficultyGraph(WorkingBeatmap workingBeatmap);
    }
}
