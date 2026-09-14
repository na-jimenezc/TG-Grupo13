using UnityEngine;
using TMPro;

public class CorteActivityManager : ActivityManagerBase
{
    public static CorteActivityManager Instance { get; private set; }

    [SerializeField] private int metaCortes = 15;
    [SerializeField] private TMP_Text contadorText;

    public int CortesRealizados { get; private set; } = 0;
    public override bool ActividadCompletada => CortesRealizados >= metaCortes;

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

    public void RegistrarCorte()
    {
        if (ActividadCompletada) return;

        CortesRealizados++;
        ActualizarUI();

        if (ActividadCompletada)
            MarcarCompletada();
    }

    private void ActualizarUI()
    {
        if (contadorText != null)
            contadorText.text = $"{CortesRealizados}/{metaCortes}";
    }
}