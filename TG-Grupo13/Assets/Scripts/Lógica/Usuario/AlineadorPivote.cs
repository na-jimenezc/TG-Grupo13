using UnityEngine;

public static class AlineadorPivote
{
    public static void CorregirPivote(GameObject objeto)
    {
        Renderer[] renderers = objeto.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        Vector3 offset = objeto.transform.position - bounds.center;
        objeto.transform.position += offset;
    }
}