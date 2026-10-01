using System.Collections;
using UnityEngine;

public class ManoIzquierdaController : MonoBehaviour
{
    [SerializeField] private HombroAcumulador acumulador;
    [Tooltip("Ramos que deben estar apilados para poder descargar. Debe coincidir con 'Ramos Para Completar' del CorteActivityManager.")]
    [SerializeField] private int ramosRequeridos = 5;
    [Tooltip("Segundos que se espera, con el balde lleno a la vista, antes de avanzar a la siguiente actividad.")]
    [SerializeField] private float retardoAntesDeAvanzar = 1.5f;

    private int _zonasBaldeDentro = 0; //Contador: tolera varios colliders / entradas y salidas seguidas
    private BaldeContenedor _baldeActual; //Balde en el que está la mano ahora mismo

    private void Update()
    {
        if (_zonasBaldeDentro <= 0 || acumulador == null) return;
        if (acumulador.CantidadApilada < ramosRequeridos) return;

        //Solo el botón de la mano IZQUIERDA
        if (!acumulador.BotonSoltado) return;

        acumulador.DepositarTodoEnBalde();
        _zonasBaldeDentro = 0;

        //Muestra el prefab del balde con los lirios
        if (_baldeActual != null) _baldeActual.MostrarLleno();

        //Se espera un momento antes de avanzar, para que el jugador vea el resultado
        StartCoroutine(AvanzarTrasRetardo());
    }

    private IEnumerator AvanzarTrasRetardo()
    {
        yield return new WaitForSeconds(retardoAntesDeAvanzar);
        if (CorteActivityManager.Instance != null)
            CorteActivityManager.Instance.RegistrarRecoleccionFinal();
    }

    private void OnTriggerEnter(Collider other)
    {
        var zona = other.GetComponentInParent<ZonaDeposito>();
        if (zona == null || zona.EsZonaDeRamos) return;

        _zonasBaldeDentro++;
        var balde = zona.GetComponentInParent<BaldeContenedor>();
        if (balde != null) _baldeActual = balde;
    }

    private void OnTriggerExit(Collider other)
    {
        var zona = other.GetComponentInParent<ZonaDeposito>();
        if (zona != null && !zona.EsZonaDeRamos)
            _zonasBaldeDentro = Mathf.Max(0, _zonasBaldeDentro - 1);
    }
}