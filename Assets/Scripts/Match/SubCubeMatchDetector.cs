using System.Collections.Generic;
using MyGame.Board;
using MyGame.Interaction;

namespace MyGame.Match
{
    /// <summary>
    /// Scans rows and columns of the board's sub-grid for runs of same-colored
    /// sub-slots. Only checks the color data stored in the sub-grid.
    /// A run counts once per distinct sub-cube (wide sub-cubes deduped).
    /// </summary>
    public class SubCubeMatchDetector
    {
        public List<MatchResult> FindMatches(BoardSubGrid subGrid, int minSize = 3)
        {
            var results = new List<MatchResult>();
            FindMatches(subGrid, results, minSize);
            return results;
        }

        public void FindMatches(BoardSubGrid subGrid, List<MatchResult> results, int minSize = 3)
        {
            results.Clear();
            if (subGrid == null || subGrid.SubWidth == 0 || subGrid.SubHeight == 0) return;

            ScanRows(subGrid, results, minSize);
            ScanColumns(subGrid, results, minSize);
        }

        private void ScanRows(BoardSubGrid subGrid, List<MatchResult> results, int minSize)
        {
            int w = subGrid.SubWidth;
            int h = subGrid.SubHeight;

            for (int y = 0; y < h; y++)
            {
                int x = 0;
                while (x < w)
                {
                    var color = subGrid.GetColor(x, y);
                    if (color == CubeColor.None) { x++; continue; }

                    var run = new List<SubCube>();
                    var seen = new HashSet<SubCube>();
                    int startX = x;

                    while (x < w)
                    {
                        var nextColor = subGrid.GetColor(x, y);
                        if (nextColor != color) break;

                        var sub = subGrid.GetSubCube(x, y);
                        if (sub != null && seen.Add(sub)) run.Add(sub);

                        x++;
                    }

                    if (run.Count >= minSize)
                        results.Add(new MatchResult(color, run, true, false));

                    if (x == startX) x++;
                }
            }
        }

        private void ScanColumns(BoardSubGrid subGrid, List<MatchResult> results, int minSize)
        {
            int w = subGrid.SubWidth;
            int h = subGrid.SubHeight;

            for (int x = 0; x < w; x++)
            {
                int y = 0;
                while (y < h)
                {
                    var color = subGrid.GetColor(x, y);
                    if (color == CubeColor.None) { y++; continue; }

                    var run = new List<SubCube>();
                    var seen = new HashSet<SubCube>();
                    int startY = y;

                    while (y < h)
                    {
                        var nextColor = subGrid.GetColor(x, y);
                        if (nextColor != color) break;

                        var sub = subGrid.GetSubCube(x, y);
                        if (sub != null && seen.Add(sub)) run.Add(sub);

                        y++;
                    }

                    if (run.Count >= minSize)
                        results.Add(new MatchResult(color, run, false, true));

                    if (y == startY) y++;
                }
            }
        }
    }
}