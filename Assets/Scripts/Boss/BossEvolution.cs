using System.Collections;
using UnityEngine;

/// <summary>Presentation swap: when the caterpillar fills up, hide its body and reveal the butterfly.</summary>
[RequireComponent(typeof(BossHunger))]
public class BossEvolution : MonoBehaviour
{
    [SerializeField] private CaterpillarBoss caterpillar;
    [SerializeField] private GameObject[] caterpillarParts;
    [SerializeField] private ButterflyBoss butterfly;
    [SerializeField] private GameObject butterflyBody;
    [Tooltip("Seconds the butterfly hovers in place after appearing, so the player can react before it chases.")]
    [SerializeField] private float reactionSeconds = 3f;

    private BossHunger hunger;

    private void Awake() { hunger = GetComponent<BossHunger>(); }
    private void OnEnable() { if (hunger != null) hunger.OnBossEvolved += HandleEvolved; }
    private void OnDisable() { if (hunger != null) hunger.OnBossEvolved -= HandleEvolved; }

    private void HandleEvolved(BossData next)
    {
        bool wasAwake = caterpillar != null && caterpillar.IsAwake;
        if (caterpillar != null) { caterpillar.Sleep(); caterpillar.enabled = false; }
        foreach (GameObject part in caterpillarParts) if (part != null) part.SetActive(false);

        if (butterflyBody != null) butterflyBody.SetActive(true);
        if (butterfly != null)
        {
            butterfly.enabled = true;
            if (wasAwake) StartCoroutine(WakeAfterReveal());
        }
    }

    private IEnumerator WakeAfterReveal()
    {
        yield return new WaitForSeconds(reactionSeconds);
        if (butterfly != null) butterfly.Wake();
    }
}
