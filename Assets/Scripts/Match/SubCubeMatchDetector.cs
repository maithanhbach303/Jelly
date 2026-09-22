using System.Collections.Generic;
using MyGame.Board;
using MyGame.Interaction;

namespace MyGame.Match
{
    /// <summary>
    /// Row/column scan-based match detection over the board's sub-grid.
    /// Supports both allocating (returns a new List) and non-allocating
    /// (fills a caller-provided List) forms.
    /// </summary>
    public class SubCubeMatchDetector
    {
        #region Allocating API

        public List<MatchResult> FindMatches(BoardSubGrid subGrid, int minSize = 2)
        {
            var results = new List<MatchResult>();
            FindMatches(subGrid, results, minSize);
            return results;
        }

        public List<MatchResult> FindMatches(GridManager grid, int minSize = 2)
            => FindMatches(grid != null ? grid.SubGrid : null, minSize);

        #endregion

        #region Non-Allocating API

        /// <summary>
        /// Fills the provided list with all matches found. The list is cleared first.
        /// Use this to avoid a fresh allocation per detection pass.
        /// </summary>
        public void FindMatches(BoardSubGrid subGrid, List<MatchResult> output, int minSize = 2)
        {
            if (output == null) return;
            output.Clear();

            if (subGrid == null || subGrid.SubWidth == 0 || subGrid.SubHeight == 0)
                return;

            ScanRows(subGrid, minSize, output);
            ScanColumns(subGrid, minSize, output);
        }

        public void FindMatches(GridManager grid, List<MatchResult> output, int minSize = 2)
            => FindMatches(grid != null ? grid.SubGrid : null, output, minSize);

        #endregion

        #region Scanners

        private void ScanRows(BoardSubGrid subGrid, int minSize, List<MatchResult> results)
        {
            int w = subGrid.SubWidth;
            int h = subGrid.SubHeight;

            for (int y = 0; y < h; y++)
            {
                int x = 0;
                while (x < w)
                {
                    var startCube = subGrid.Get(x, y);
                    if (startCube == null || startCube.CurrentColor == CubeColor.None)
                    {
                        x++;
                        continue;
                    }

                    var color = startCube.CurrentColor;
                    var run = new List<SubCube> { startCube };
                    x++;

                    while (x < w)
                    {
                        var next = subGrid.Get(x, y);
                        if (next == null) break;
                        if (next == startCube) break;    // same wide sub-cube
                        if (next.CurrentColor != color) break;

                        run.Add(next);
                        x++;
                    }

                    if (run.Count >= minSize)
                        results.Add(new MatchResult(color, run, isHorizontal: true, isVertical: false));
                }
            }
        }

        private void ScanColumns(BoardSubGrid subGrid, int minSize, List<MatchResult> results)
        {
            int w = subGrid.SubWidth;
            int h = subGrid.SubHeight;

            for (int x = 0; x < w; x++)
            {
                int y = 0;
                while (y < h)
                {
                    var startCube = subGrid.Get(x, y);
                    if (startCube == null || startCube.CurrentColor == CubeColor.None)
                    {
                        y++;
                        continue;
                    }

                    var color = startCube.CurrentColor;
                    var run = new List<SubCube> { startCube };
                    y++;

                    while (y < h)
                    {
                        var next = subGrid.Get(x, y);
                        if (next == null) break;
                        if (next == startCube) break;
                        if (next.CurrentColor != color) break;

                        run.Add(next);
                        y++;
                    }

                    if (run.Count >= minSize)
                        results.Add(new MatchResult(color, run, isHorizontal: false, isVertical: true));
                }
            }
        }

        #endregion
    }
}