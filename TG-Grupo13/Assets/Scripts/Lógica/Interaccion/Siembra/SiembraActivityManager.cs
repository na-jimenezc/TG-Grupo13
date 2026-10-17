using UnityEngine;
using TMPro;

/*Administrador de la actividad de Siembra. Controla el objetivo numérico de plantas,
actualiza la interfaz de progreso y notifica cuando se cumple la meta*/
public class SiembraActivityManager : ActivityManagerBase
{
    //Singleton para acceso global a la instancia del administrador de la actividad
    public static SiembraActivityManager Instance { get; private set; }

    //Meta de plantas a sembrar para completar la actividad
    [SerializeField] private int metaPlantas = 10;
    [SerializeField] private TMP_Text contadorText;

    //Contador de plantas sembradas hasta el momento
    public int PlantasSembradas { get; private set; } = 0;
    public override bool ActividadCompletada => PlantasSembradas >= metaPlantas;
    public override string ResumenProgreso => $"Plantas sembradas: {PlantasSembradas}/{metaPlantas}";
    public override float FraccionProgreso => metaPlantas > 0 ? Mathf.Clamp01((float)PlantasSembradas / metaPlantas) : 1f;

    protected override void Awake()
    {
        base.Awake();
        Instance = this;
        ActualizarUI(); //Actualiza la interfaz de usuario al iniciar la actividad
    }

    //Limpia la instancia del singleton al destruir el objeto
    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (Instance == this) Instance = null;
    }

    //Registra una planta sembrada, actualiza el contador y verifica si se ha completado la actividad
    public void RegistrarPlantaSembrada()
    {
        //Evita seguir contando si la meta ya se cumplió
        if (ActividadCompletada) return;

        PlantasSembradas++;
        ActualizarUI();

        //Si con este incremento se alcanzó la meta, dispara el evento de completitud
        if (ActividadCompletada)
            MarcarCompletada();
    }

    //Actualiza la interfaz de usuario con el progreso actual de plantas sembradas
    private void ActualizarUI()
    {
        if (contadorText != null)
            contadorText.text = $"{PlantasSembradas}/{metaPlantas}";
        NotificarProgreso();
    }
}