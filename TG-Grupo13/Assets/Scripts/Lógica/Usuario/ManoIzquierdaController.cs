using System.Collections;
using UnityEngine;

public class ManoIzquierdaController : MonoBehaviour
{
    [SerializeField] private HombroAcumulador acumulador;
    [Tooltip("Ramos que deben estar apilados para poder descargar. Coincide con Ramos Para Completar del Manager")]
    [SerializeField] private int ramosRequeridos = 5;
    [Tooltip("Segundos que se espera antes de avanzar a la siguiente actividad")]
    [SerializeField] private float retardoAntesDeAvanzar = 1.5f;

    private int _zonasBaldeDentro = 0; //Contador para controlar entradas y salidas del trigger del balde
    private BaldeContenedor _baldeActual; //Balde en el que está la mano ahora mismo

    private void Update()
    {
        if (_zonasBaldeDentro <= 0 || acumulador == null) return;
        if (acumulador.CantidadApilada < ramosRequeridos) return;

        //Solo cuenta soltar el botón de agarre
        if (!acumulador.BotonSoltado) return;

        acumulador.DepositarTodoEnBalde();
        _zonasBaldeDentro = 0;

        //Muestra el prefab del balde con los lirios
        if (_baldeActual != null) _baldeActual.MostrarLleno();

        //Se espera un momento antes de avanzar, para que el jugador vea el resultado
        StartCoroutine(AvanzarTrasRetardo());
    }

    //Manda el mensaje para indicar que se puede pasar a la siguiente actividad o terminar la simulación
    private IEnumerator AvanzarTrasRetardo()
    {
        yield return new WaitForSeconds(retardoAntesDeAvanzar);
        if (CorteActivityManager.Instance != null)
            CorteActivityManager.Instance.RegistrarRecoleccionFinal();
    }

    //Detecta si la mano entra en el trigger de un balde y si es así, aumenta el contador de zonas dentro y guarda el balde actual
    private void OnTriggerEnter(Collider other)
    {
        var zona = other.GetComponentInParent<ZonaDeposito>();
        if (zona == null || zona.EsZonaDeRamos) return;

        _zonasBaldeDentro++;
        var balde = zona.GetComponentInParent<BaldeContenedor>();
        if (balde != null) _baldeActual = balde;
    }

    //Detecta si la mano sale del trigger de un balde y si es así, disminuye el contador de zonas dentro
    private void OnTriggerExit(Collider other)
    {
        var zona = other.GetComponentInParent<ZonaDeposito>();
        if (zona != null && !zona.EsZonaDeRamos)
            _zonasBaldeDentro = Mathf.Max(0, _zonasBaldeDentro - 1);
    }
}