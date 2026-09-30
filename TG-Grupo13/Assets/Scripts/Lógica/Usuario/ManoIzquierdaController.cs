using UnityEngine;

public class ManoIzquierdaController : MonoBehaviour
{
    [SerializeField] private HombroAcumulador acumulador;
    [Tooltip("Ramos que deben estar apilados para poder descargar. Debe coincidir con 'Ramos Para Completar' del CorteActivityManager.")]
    [SerializeField] private int ramosRequeridos = 5;

    private int _zonasBaldeDentro = 0; //Contador: tolera varios colliders / entradas y salidas seguidas

    private void Update()
    {
        if (_zonasBaldeDentro <= 0 || acumulador == null) return;
        if (acumulador.CantidadApilada < ramosRequeridos) return;

        if (!acumulador.BotonSoltado) return;

        acumulador.DepositarTodoEnBalde();
        CorteActivityManager.Instance.RegistrarRecoleccionFinal();
        _zonasBaldeDentro = 0;
    }

    private void OnTriggerEnter(Collider other)
    {
        var zona = other.GetComponentInParent<ZonaDeposito>();
        if (zona != null && !zona.EsZonaDeRamos) _zonasBaldeDentro++;
    }

    private void OnTriggerExit(Collider other)
    {
        var zona = other.GetComponentInParent<ZonaDeposito>();
        if (zona != null && !zona.EsZonaDeRamos) _zonasBaldeDentro = Mathf.Max(0, _zonasBaldeDentro - 1);
    }
}