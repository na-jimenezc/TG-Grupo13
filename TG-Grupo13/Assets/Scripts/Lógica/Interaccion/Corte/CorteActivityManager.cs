using UnityEngine;
using TMPro;

public class CorteActivityManager : ActivityManagerBase
{
    public static CorteActivityManager Instance { get; private set; }

    [Header("Metas")]
    [SerializeField] private int tallosParaRamo = 5;
    [SerializeField] private int ramosParaCompletar = 5; 
    [SerializeField] private TMP_Text contadorTallosText;
    [SerializeField] private TMP_Text contadorRamosText;

    public int TallosEnRamoActual { get; private set; } = 0;
    public int RamosCompletados { get; private set; } = 0;

    /*La actividad se termina cuando pones todos los ramos en el balde, pero se añade una 
     fase extra para determinar cuantos ramos listos existen*/
    public bool RamosListos => RamosCompletados >= ramosParaCompletar;
    public override bool ActividadCompletada => RamosListos && RecoleccionFinalCompletada;

    public bool RecoleccionFinalCompletada { get; private set; } = false;

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

    public void RegistrarTalloDeshojado()
    {
        if (TallosEnRamoActual >= tallosParaRamo) return;
        TallosEnRamoActual++;
        ActualizarUI();
    }

    //Llamado al finalizar el corte parejo + colocación del capuchón
    public void RegistrarRamoCompletado()
    {
        if (RamosListos) return;

        //Se reinicia el compartimiento de tallos y se aumenta el contador de ramos
        RamosCompletados++;
        TallosEnRamoActual = 0; 
        ActualizarUI();
    }

    public void RegistrarRecoleccionFinal()
    {
        if (RecoleccionFinalCompletada) return;

        RecoleccionFinalCompletada = true;
        if (ActividadCompletada)
            MarcarCompletada();
    }

    private void ActualizarUI()
    {
        if (contadorTallosText != null)
            contadorTallosText.text = $"Tallos: {TallosEnRamoActual}/{tallosParaRamo}";
        if (contadorRamosText != null)
            contadorRamosText.text = $"Ramos: {RamosCompletados}/{ramosParaCompletar}";
    }
}