using UnityEngine;
using TMPro;

public class SiembraActivityManager : ActivityManagerBase
{
    public static SiembraActivityManager Instance { get; private set; }

    [SerializeField] private int metaPlantas = 10;
    [SerializeField] private TMP_Text contadorText;

    public int PlantasSembradas { get; private set; } = 0;
    public override bool ActividadCompletada => PlantasSembradas >= metaPlantas;

    protected override void Awake()
    {
        base.Awake();
        Instance = this;
        ActualizarUI();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (Instance == this) Instance = null;
    }

    public void RegistrarPlantaSembrada()
    {
        if (ActividadCompletada) return;

        PlantasSembradas++;
        ActualizarUI();

        if (ActividadCompletada)
            MarcarCompletada();
    }

    private void ActualizarUI()
    {
        if (contadorText != null)
            contadorText.text = $"{PlantasSembradas}/{metaPlantas}";
    }
}