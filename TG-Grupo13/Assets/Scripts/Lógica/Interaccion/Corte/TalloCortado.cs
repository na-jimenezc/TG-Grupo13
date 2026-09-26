using UnityEngine;

public class TalloCortado : MonoBehaviour
{
    [SerializeField] private GameObject modeloConHojas;
    [SerializeField] private GameObject modeloDeshojado;
    [SerializeField] private Collider zonaDeshoje;

    private bool _manoDentro = false;
    private bool _deshojado = false;

    private void Awake()
    {
        modeloConHojas.SetActive(true);
        modeloDeshojado.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_deshojado) return;
        if (other.CompareTag("ManoDominante")) _manoDentro = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("ManoDominante")) _manoDentro = false;
    }

    private void Update()
    {
        if (_deshojado || !_manoDentro) return;

        bool trigger = OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger)
                    || OVRInput.Get(OVRInput.Button.SecondaryIndexTrigger);

        if (trigger)
            Deshojar();
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