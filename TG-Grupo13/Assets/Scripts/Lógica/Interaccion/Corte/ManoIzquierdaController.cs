using UnityEngine;

public class ManoIzquierdaController : MonoBehaviour
{
    [SerializeField] private HombroAcumulador acumulador;
    private bool _dentroDelBalde = false;

    private void Update()
    {
        if (!_dentroDelBalde || acumulador.CantidadApilada == 0) return;

        bool soltando = OVRInput.GetUp(OVRInput.Button.PrimaryHandTrigger)
                      || OVRInput.GetUp(OVRInput.Button.SecondaryHandTrigger);

        if (soltando)
        {
            acumulador.DepositarTodoEnBalde();
            CorteActivityManager.Instance.RegistrarRecoleccionFinal();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<ZonaDeposito>(out var zona) && !zona.EsZonaDeRamos)
            _dentroDelBalde = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent<ZonaDeposito>(out var zona) && !zona.EsZonaDeRamos)
            _dentroDelBalde = false;
    }
}