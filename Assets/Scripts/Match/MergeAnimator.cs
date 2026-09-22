using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MyGame.Interaction;

namespace MyGame.Match
{
    /// <summary>
    /// Handles the "merge" visual: sub-cubes in a match group slide toward
    /// their shared centroid, shrink, and are destroyed at the end.
    /// </summary>
    public class MergeAnimator : MonoBehaviour
    {
        [Header("Timing")]
        [Tooltip("How long sub-cubes take to slide to the merge point.")]
        [SerializeField] private float mergeDuration = 0.22f;

        [Tooltip("Additional pop/shrink after all sub-cubes have converged.")]
        [SerializeField] private float popDuration = 0.08f;

        [Header("Motion")]
        [Tooltip("How much the sub-cubes scale down as they merge. 1 = no shrink, 0.2 = shrink to 20%.")]
        [Range(0f, 1f)]
        [SerializeField] private float endScaleFactor = 0.15f;

        [Tooltip("Optional pop target scale multiplier for the final flash.")]
        [SerializeField] private float popScale = 1.6f;

        [Header("Flash")]
        [Tooltip("If set, spawns a quick flash VFX at the merge point when the sub-cubes converge.")]
        [SerializeField] private GameObject flashPrefab;

        [Tooltip("Optional tint applied to merged sub-cubes as they shrink.")]
        [SerializeField] private Color mergeTint = Color.white;

        /// <summary>Animates one match group. Yields until the sub-cubes are destroyed.</summary>
        public IEnumerator AnimateGroup(MatchResult match, System.Action onComplete = null)
        {
            var blocks = match.Blocks;
            if (blocks == null || blocks.Count == 0)
            {
                onComplete?.Invoke();
                yield break;
            }

            // 1. Compute merge point (world centroid of surviving sub-cubes)
            Vector3 centroid = Vector3.zero;
            int liveCount = 0;
            var live = new List<SubCube>(blocks.Count);

            for (int i = 0; i < blocks.Count; i++)
            {
                var sub = blocks[i];
                if (sub == null) continue;
                centroid += sub.transform.position;
                liveCount++;
                live.Add(sub);
            }

            if (liveCount == 0)
            {
                onComplete?.Invoke();
                yield break;
            }

            centroid /= liveCount;

            // 2. Snapshot start positions and scales
            var startPositions = new Vector3[live.Count];
            var startScales = new Vector3[live.Count];
            for (int i = 0; i < live.Count; i++)
            {
                startPositions[i] = live[i].transform.position;
                startScales[i] = live[i].transform.localScale;
            }

            // 3. Slide all sub-cubes toward the centroid, shrinking them
            float t = 0f;
            while (t < mergeDuration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / mergeDuration);
                float ease = EaseInQuad(k);

                for (int i = 0; i < live.Count; i++)
                {
                    var sub = live[i];
                    if (sub == null) continue;

                    sub.transform.position = Vector3.Lerp(startPositions[i], centroid, ease);

                    float scaleK = Mathf.Lerp(1f, endScaleFactor, k);
                    sub.transform.localScale = startScales[i] * scaleK;
                }

                yield return null;
            }

            // 4. Snap to centroid (in case of interpolation drift)
            for (int i = 0; i < live.Count; i++)
            {
                var sub = live[i];
                if (sub == null) continue;
                sub.transform.position = centroid;
            }

            // 5. Optional flash / pop
            if (flashPrefab != null)
                Instantiate(flashPrefab, centroid, Quaternion.identity);

            if (popDuration > 0f)
            {
                t = 0f;
                while (t < popDuration)
                {
                    t += Time.deltaTime;
                    float k = Mathf.Clamp01(t / popDuration);
                    float scaleK = Mathf.Lerp(endScaleFactor, popScale, k);

                    for (int i = 0; i < live.Count; i++)
                    {
                        var sub = live[i];
                        if (sub == null) continue;
                        sub.transform.localScale = startScales[i] * scaleK;
                    }

                    yield return null;
                }
            }

            // 6. Destroy all sub-cubes
            for (int i = 0; i < live.Count; i++)
            {
                var sub = live[i];
                if (sub == null) continue;
                sub.enabled = false;
                Destroy(sub.gameObject);
            }

            onComplete?.Invoke();
        }

        /// <summary>Animate every group in a match batch in parallel.</summary>
        public IEnumerator AnimateBatch(IReadOnlyList<MatchResult> matches, System.Action onComplete = null)
        {
            if (matches == null || matches.Count == 0)
            {
                onComplete?.Invoke();
                yield break;
            }

            int running = matches.Count;
            for (int i = 0; i < matches.Count; i++)
            {
                var m = matches[i];
                StartCoroutine(AnimateGroup(m, () => running--));
            }

            while (running > 0) yield return null;

            onComplete?.Invoke();
        }

        private static float EaseInQuad(float x) => x * x;
    }
}