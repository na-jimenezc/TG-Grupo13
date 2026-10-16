using UnityEngine;
using System;

//Objeto que representa un tallo de lirio en la plantación, que puede ser cortado por el usuario
public class TalloEnPlantacion : MonoBehaviour
{
    [SerializeField] private GameObject prefabTalloCortado; //MODELO DEL LIRIO CORTADO
    private bool _cortado = false;

    [Header("Efectos de Sonido")]
    [SerializeField] private AudioClip sonidoCorte;

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
        if (manoSecundaria != null && !manoSecundaria.TieneAlgoEnMano)
        {
            //Si la mano izquierda está libre, spawnea el tallo cortado directamente en ella
            var talloCortado = Instantiate(prefabTalloCortado, manoSecundaria.PuntoAnclaje.position, manoSecundaria.PuntoAnclaje.rotation);
            manoSecundaria.Sostener(talloCortado);
        }

        gameObject.SetActive(false); //Limpieza síncrona antes de desaparecer la planta original
        Destroy(gameObject, 0.1f);
    }
}