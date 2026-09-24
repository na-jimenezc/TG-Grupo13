using System.Collections.Generic;
using UnityEngine;

public class RamoEnProgreso : MonoBehaviour
{
    public static RamoEnProgreso Instance { get; private set; }

    [SerializeField] private Transform puntoAgrupacion; //PUNTO PARA JUNTAR TALLOS
    [SerializeField] private int tallosParaNivelar = 5;
    [SerializeField] private GameObject prefabCapuchon; //PREFAB DEL CAPUCHÓN QUE SE PUEDE TOMAR DESPUÉS DE NIVELAR

    private readonly List<TalloCortado> _tallos = new List<TalloCortado>();
    public bool Nivelado { get; private set; } = false;

    private void Awake() => Instance = this;

    public void AgregarTallo(TalloCortado tallo)
    {
        // Saca el tallo de la mano y lo agrupa en el punto del ramo en progreso
        ManoSecundaria.Instance.Soltar();
        tallo.transform.SetParent(puntoAgrupacion, worldPositionStays: false);

        // Distribución simple en abanico para que se vean como manojo, no superpuestos
        int index = _tallos.Count;
        tallo.transform.localPosition = new Vector3(index * 0.015f, 0, 0);

        _tallos.Add(tallo);
    }

    public void CortarParejo()
    {
        if (Nivelado) return;
        if (_tallos.Count < tallosParaNivelar) return; // aún no hay suficientes tallos

        Nivelado = true;
        //HACER SONIDO

        //SPAWNEAR EL CAPUCHÓN EN EL PUNTO DE AGRUPACIÓN
        if (prefabCapuchon != null)
            Instantiate(prefabCapuchon, puntoAgrupacion.position, Quaternion.identity);
    }

    public bool PuedeArmarseConCapuchon => Nivelado;

    public List<TalloCortado> Tallos => _tallos;

    public void Reiniciar()
    {
        foreach (var t in _tallos)
            if (t != null) Destroy(t.gameObject); // ya se consumieron al armar el ramo terminado

        _tallos.Clear();
        Nivelado = false;
    }
}