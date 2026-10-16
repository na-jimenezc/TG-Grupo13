using UnityEngine;

public static class AlineadorPivote
{
    //Acomoda el pivote del objeto al centro de su malla, ajustando su posición en el mundo para que no se mueva visualmente.
    public static void CorregirPivote(GameObject objeto)
    {
        //Obtiene todos los renderers del objeto y calcula su bounds combinado
        Renderer[] renderers = objeto.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        //Calcula el bounds combinado de todos los renderers
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        //Calcula el offset entre el centro del bounds y la posición actual del objeto
        Vector3 offset = objeto.transform.position - bounds.center;
        objeto.transform.position += offset;
    }
}