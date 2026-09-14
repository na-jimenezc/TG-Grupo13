using UnityEngine;
using TMPro;

public class DeshierbeActivityManager : ActivityManagerBase
{
    public static DeshierbeActivityManager Instance { get; private set; }

    [SerializeField] private int metaOrtigas = 20;
    [SerializeField] private TMP_Text contadorText;

    public int OrtigasRecolectadas { get; private set; } = 0;
    public override bool ActividadCompletada => OrtigasRecolectadas >= metaOrtigas;

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

    public void RegistrarOrtigaDepositada()
    {
        if (ActividadCompletada) return;

        OrtigasRecolectadas++;
        ActualizarUI();

        if (ActividadCompletada)
            MarcarCompletada();
    }

    private void ActualizarUI()
    {
        if (contadorText != null)
            contadorText.text = $"{OrtigasRecolectadas}/{metaOrtigas}";
    }
}