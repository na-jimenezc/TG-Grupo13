using UnityEngine;
using System;
using System.Collections;

//Objeto que representa un tallo de lirio en la plantación, que puede ser cortado por el usuario
public class TalloEnPlantacion : MonoBehaviour
{
    [SerializeField] private GameObject prefabTalloCortado; //MODELO DEL LIRIO CORTADO
    private bool _cortado = false;

    [Header("Efectos de Sonido")]
    [SerializeField] private AudioClip sonidoCorte;

    //Para la demostración de la pantalla de espera: ahí la mano secundaria vive en Core,
    //así que el tallo cortado se iría con el jugador a la siguiente escena
    [Header("Demostración (pantalla de espera)")]
    [Tooltip("Si está activo, el tallo cortado aparece en la mano izquierda (actividad y tutorial de corte)")]
    [SerializeField] private bool entregarTalloEnMano = true;
    [Tooltip("Segundos para que el lirio vuelva a aparecer después de cortarlo. 0 = se destruye (comportamiento normal)")]
    [SerializeField] private float segundosParaReaparecer = 0f;

    //EVENTO DEL TUTORIAL (estático porque hay muchos tallos en la plantación)
    public static event Action OnCortado;

    //Ejecuta la acción de corte sobre la planta viva.
    public void Cortar()
    {
        if (_cortado) return;
        _cortado = true;
        OnCortado?.Invoke();

        //Reproduce el sonido de corte en la posición del tallo
        if (sonidoCorte != null)
            AudioSource.PlayClipAtPoint(sonidoCorte, transform.position);

        var manoSecundaria = ManoSecundaria.Instance;
        if (entregarTalloEnMano && manoSecundaria != null && !manoSecundaria.TieneAlgoEnMano)
        {
            //Si la mano izquierda está libre, spawnea el tallo cortado directamente en ella
            var talloCortado = Instantiate(prefabTalloCortado, manoSecundaria.PuntoAnclaje.position, manoSecundaria.PuntoAnclaje.rotation);
            manoSecundaria.Sostener(talloCortado);
        }

        if (segundosParaReaparecer > 0f)
        {
            StartCoroutine(Reaparecer());
            return;
        }

        gameObject.SetActive(false); //Limpieza síncrona antes de desaparecer la planta original
        Destroy(gameObject, 0.1f);
    }

#if UNITY_EDITOR
    // SOLO EN EL EDITOR: en Play, clic derecho sobre este componente (o en los tres puntos) > "Cortar (prueba)"
    // para probar el sonido, el evento y la reaparición sin usar las tijeras
    [ContextMenu("Cortar (prueba)")]
    private void CortarPrueba()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[TalloEnPlantacion] Entra a Play para probar el corte", this);
            return;
        }
        Cortar();
    }
#endif

    //Oculta el lirio y lo vuelve a mostrar después de un tiempo para poder cortarlo otra vez.
    //No se desactiva el objeto porque la corrutina se detendría
    private IEnumerator Reaparecer()
    {
        var renderers = GetComponentsInChildren<Renderer>();
        var colliders = GetComponentsInChildren<Collider>();
        foreach (var r in renderers) r.enabled = false;
        foreach (var c in colliders) c.enabled = false;

        yield return new WaitForSeconds(segundosParaReaparecer);

        foreach (var r in renderers) r.enabled = true;
        foreach (var c in colliders) c.enabled = true;
        _cortado = false;
    }
}