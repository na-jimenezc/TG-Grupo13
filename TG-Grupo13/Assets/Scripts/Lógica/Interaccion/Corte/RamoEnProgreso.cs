using System.Collections.Generic;
using UnityEngine;

public class RamoEnProgreso : MonoBehaviour
{
    public static RamoEnProgreso Instance { get; private set; }

    [SerializeField] private Transform puntoAgrupacion;
    [SerializeField] private int tallosParaNivelar = 5;

    private readonly List<TalloCortado> _tallos = new List<TalloCortado>();
    public bool Nivelado { get; private set; } = false;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        // Se ejecuta en Start (no en Awake) para garantizar que ManoSecundaria,
        // que vive en Core y se carga antes, ya haya corrido su propio Awake.
        AnclarseALaMano();
    }

    private void AnclarseALaMano()
    {
        if (ManoSecundaria.Instance == null)
        {
            Debug.LogWarning("[RamoEnProgreso] ManoSecundaria.Instance es null — no se pudo anclar a la mano.");
            return;
        }

        transform.SetParent(ManoSecundaria.Instance.transform, worldPositionStays: false);
        transform.localPosition = new Vector3(0f, 0.05f, 0f); //REVISAR SI QUEDÓ EN LA PALMA
        transform.localRotation = Quaternion.identity;
    }

    public void AgregarTallo(TalloCortado tallo)
    {
        ManoSecundaria.Instance.Soltar();
        tallo.transform.SetParent(puntoAgrupacion, worldPositionStays: false);

        int index = _tallos.Count;
        tallo.transform.localPosition = new Vector3(index * 0.015f, 0, 0);

        _tallos.Add(tallo);
    }

    public void CortarParejo()
    {
        if (Nivelado) return;
        if (_tallos.Count < tallosParaNivelar) return;

        Nivelado = true;
        //HACER EL SONIDO DE NIVELADO
    }

    public bool PuedeArmarseConCapuchon => Nivelado;

    public List<TalloCortado> Tallos => _tallos;

    public void Reiniciar()
    {
        foreach (var t in _tallos)
            if (t != null) Destroy(t.gameObject);

        _tallos.Clear();
        Nivelado = false;
    }
}