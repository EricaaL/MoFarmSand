using UnityEngine;

public class AnimalCrossingCamera : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f, 9f, -10f);
    [SerializeField] private Vector3 eulerAngles = new Vector3(40f, 0f, 0f);
    [SerializeField] private float smoothTime = 0.2f;

    private Vector3 velocity;

    private void Start()
    {
        if (target == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) target = p.transform;
        }
        if (target != null) transform.position = target.position + offset;
        transform.rotation = Quaternion.Euler(eulerAngles);
    }

    private void LateUpdate()
    {
        if (target == null) return;
        transform.position = Vector3.SmoothDamp(transform.position, target.position + offset, ref velocity, smoothTime);
        transform.rotation = Quaternion.Euler(eulerAngles);
    }
}
