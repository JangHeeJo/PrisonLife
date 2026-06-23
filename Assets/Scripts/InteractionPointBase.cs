using UnityEngine;

[RequireComponent(typeof(Collider))]
public abstract class InteractionPointBase : MonoBehaviour
{
    [SerializeField] private float interactInterval = 0.08f;

    private PlayerCarryStack currentPlayer;
    private float timer;

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void Update()
    {
        if (currentPlayer == null)
            return;

        timer += Time.deltaTime;

        if (timer < interactInterval)
            return;

        timer = 0f;
        TryInteract(currentPlayer);
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerCarryStack player = other.GetComponentInParent<PlayerCarryStack>();

        if (player == null)
            return;

        currentPlayer = player;
        timer = 0f;
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerCarryStack player = other.GetComponentInParent<PlayerCarryStack>();

        if (player != currentPlayer)
            return;

        currentPlayer = null;
        timer = 0f;
    }

    protected abstract bool TryInteract(PlayerCarryStack playerCarryStack);

#if UNITY_EDITOR
    private void OnValidate()
    {
        interactInterval = Mathf.Max(0.01f, interactInterval);

        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
    }
#endif
}