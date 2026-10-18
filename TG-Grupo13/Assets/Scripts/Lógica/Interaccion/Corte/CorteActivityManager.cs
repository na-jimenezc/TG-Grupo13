using UnityEngine;
using TMPro;

/*Es el administrador de la actividad de corte,
 * controla el flujo de la recolección, armado de ramos y la fase final del depósito en el balde*/
public class CorteActivityManager : ActivityManagerBase
{
    //Instancia global (Singleton) para acceder al manager desde otros scripts
    public static CorteActivityManager Instance { get; private set; }

    [Header("Metas")]
    [SerializeField] private int tallosParaRamo = 5; //Tallos que se necesitan para completar un ramo
    [SerializeField] private int ramosParaCompletar = 5;  //Ramos que se necesitan para completar la actividad
    [SerializeField] private TMP_Text contadorTallosText;
    [SerializeField] private TMP_Text contadorRamosText;

    //Única fuente de las metas de Corte: RamoEnProgreso y ManoIzquierdaController las leen de aquí
    public int TallosPorRamo => tallosParaRamo;
    public int RamosParaCompletar => ramosParaCompletar;

    //Contadores de progreso
    public int TallosEnRamoActual { get; private set; } = 0;
    public int RamosCompletados { get; private set; } = 0;

    /*La actividad se termina cuando pones todos los ramos en el balde, pero se añade una 
     fase extra para determinar cuantos ramos listos existen*/
    public bool RamosListos => RamosCompletados >= ramosParaCompletar;

    //Sobrescribe la propiedad de la clase base para alertar el estado actual
    public override bool ActividadCompletada => RamosListos && RecoleccionFinalCompletada;

    // Bandera para saber si ya se depositaron los ramos en el balde final
    public bool RecoleccionFinalCompletada { get; private set; } = false;

    public override string ResumenProgreso
    {
        get
        {
            if (ActividadCompletada) return $"Ramos: {RamosCompletados}/{ramosParaCompletar} - depositados en el balde";
            if (RamosListos) return $"Ramos: {RamosCompletados}/{ramosParaCompletar} - falta depositarlos en el balde";
            return $"Ramos: {RamosCompletados}/{ramosParaCompletar}   |   Tallos del ramo actual: {TallosEnRamoActual}/{tallosParaRamo}";
        }
    }

    //Se cuenta por tallos para que la barra avance con cada tallo; el depósito final es lo único que la lleva al 100%.
    public override float FraccionProgreso
    {
        get
        {
            if (ActividadCompletada) return 1f;
            int totalTallos = tallosParaRamo * ramosParaCompletar;
            if (totalTallos <= 0) return 0f;
            return Mathf.Min(0.95f, (float)(RamosCompletados * tallosParaRamo + TallosEnRamoActual) / totalTallos);
        }
    }

    protected override void Awake()
    {
        base.Awake();
        Instance = this;
        ActualizarUI(); //Refresca la UI al iniciar la actividad
    }

    //Se asegura de limpiar la instancia al destruirse el objeto
    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (Instance == this) Instance = null;
    }

    //Se llama cada vez que el usuario deshoja un tallo cortado
    public void RegistrarTalloDeshojado()
    {
        //En caso de que el tamo esté lleno, no se puede agregar más tallos
        if (TallosEnRamoActual >= tallosParaRamo) return;
        TallosEnRamoActual++;
        ActualizarUI();
    }

    //Llamado al finalizar el corte parejo + colocación del capuchón
    public void RegistrarRamoCompletado()
    {
        //Evita sumar más ramos si ya se completó la meta de ramos
        if (RamosListos) return;

        //Se reinicia el compartimiento de tallos y se aumenta el contador de ramos
        RamosCompletados++;
        TallosEnRamoActual = 0; 
        ActualizarUI();
    }

    //Se llama al depositar todos los ramos en el balde final
    public void RegistrarRecoleccionFinal()
    {
        if (RecoleccionFinalCompletada) return;

        //Marca que la recolección final se completó
        RecoleccionFinalCompletada = true;
        NotificarProgreso();
        if (ActividadCompletada)
            MarcarCompletada();
    }

    //Actualiza la UI con los contadores actuales
    private void ActualizarUI()
    {
        if (contadorTallosText != null)
            contadorTallosText.text = $"Tallos: {TallosEnRamoActual}/{tallosParaRamo}";
        if (contadorRamosText != null)
            contadorRamosText.text = $"Ramos: {RamosCompletados}/{ramosParaCompletar}";
        NotificarProgreso();
    }
}