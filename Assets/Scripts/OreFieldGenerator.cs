using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class OreFieldGenerator : MonoBehaviour
{
    [Header("Prefab")]
    [SerializeField] private GameObject orePrefab;

    [Header("Grid")]
    [SerializeField] private int rows = 4;
    [SerializeField] private int columns = 5;
    [SerializeField] private float spacing = 1.5f;

    [Header("Option")]
    [SerializeField] private bool centerAlign = true;
    [SerializeField] private Vector3 rotationEuler;

#if UNITY_EDITOR
    [ContextMenu("Generate Ores")]
    private void GenerateOres()
    {
        if (orePrefab == null)
        {
            Debug.LogWarning("Ore Prefab is missing.", this);
            return;
        }

        ClearOres();

        Vector3 startOffset = Vector3.zero;

        if (centerAlign)
        {
            startOffset = new Vector3(
                -(columns - 1) * spacing * 0.5f,
                0f,
                -(rows - 1) * spacing * 0.5f
            );
        }

        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                Vector3 localPosition = startOffset + new Vector3(
                    column * spacing,
                    0f,
                    row * spacing
                );

                GameObject ore = PrefabUtility.InstantiatePrefab(orePrefab, transform) as GameObject;

                if (ore == null)
                    continue;

                ore.transform.localPosition = localPosition;
                ore.transform.localRotation = Quaternion.Euler(rotationEuler);
                ore.name = $"Ore_{row}_{column}";
            }
        }

        EditorUtility.SetDirty(gameObject);
    }

    [ContextMenu("Clear Ores")]
    private void ClearOres()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }

        EditorUtility.SetDirty(gameObject);
    }
#endif
}