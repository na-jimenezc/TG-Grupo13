using UnityEngine;

public class TalloCortado : MonoBehaviour
{
    [SerializeField] private GameObject modeloConHojas;
    [SerializeField] private GameObject modeloDeshojado;
    [SerializeField] private Collider zonaDeshoje; //COLIDER PARA EL TALLO

    private bool _entroMano = false;
    private bool _deshojado = false;

    private void Awake()
    {
        modeloConHojas.SetActive(true);
        modeloDeshojado.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_deshojado) return;
        if (other.CompareTag("ManoDominante")) _entroMano = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (_deshojado) return;
        if (other.CompareTag("ManoDominante") && _entroMano)
        {
            //ACCIÓN DEL SWIPE
            Deshojar();
        }
    }

    private void Deshojar()
    {
        _deshojado = true;
        modeloConHojas.SetActive(false);
        modeloDeshojado.SetActive(true);
        // TODO: sonido + animación de deshoje

        RamoEnProgreso.Instance.AgregarTallo(this);
        CorteActivityManager.Instance.RegistrarTalloDeshojado();
    }
}