using System;
using UnityEngine;

/// <summary>
/// Swaps scene trees for their snow-covered twins while the season asks for it (winter). Each snow tree is created on
/// first use at its tree's exact transform, then the pair is just toggled. Presentation only.
/// </summary>
public class SeasonTrees : MonoBehaviour
{
    [Serializable]
    public struct Swap
    {
        [Tooltip("The normal tree already in the scene.")] public GameObject tree;
        [Tooltip("Prefab of its snow-covered twin.")] public GameObject snowPrefab;
    }

    [SerializeField] private Swap[] swaps;

    private GameObject[] snowTwins;
    private bool? snowy;

    public void SetSnowy(bool value)
    {
        if (swaps == null || snowy == value) return;
        snowy = value;
        if (value) BuildTwins();

        for (int i = 0; i < swaps.Length; i++)
        {
            if (swaps[i].tree != null) swaps[i].tree.SetActive(!value);
            if (snowTwins != null && snowTwins[i] != null) snowTwins[i].SetActive(value);
        }
    }

    private void BuildTwins()
    {
        if (snowTwins != null) return;
        snowTwins = new GameObject[swaps.Length];
        for (int i = 0; i < swaps.Length; i++)
        {
            Swap s = swaps[i];
            if (s.tree == null || s.snowPrefab == null) continue;
            Transform t = s.tree.transform;
            GameObject twin = Instantiate(s.snowPrefab, t.parent);
            twin.name = s.snowPrefab.name;
            twin.transform.SetLocalPositionAndRotation(t.localPosition, t.localRotation);
            twin.transform.localScale = t.localScale;
            snowTwins[i] = twin;
        }
    }
}
