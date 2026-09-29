using UnityEngine;
using TMPro;

/* Administrador de la actividad de Deshierbe. Controla la recolección de ortigas,
/// evalúa la meta numérica y notifica al núcleo del juego al finalizar*/
public class DeshierbeActivityManager : ActivityManagerBase
{
    //Singleton instance
    public static DeshierbeActivityManager Instance { get; private set; }

    [SerializeField] private int metaOrtigas = 20; //Cuántas malezas requiere arrancar
    [SerializeField] private TMP_Text contadorText;

    //Sobrescribe la evaluación de condición base
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

    //Método para registrar la recolección de una ortiga
    public void RegistrarOrtigaDepositada()
    {
        if (ActividadCompletada) return;

        OrtigasRecolectadas++;
        ActualizarUI();

        //Al alcanzar o superar la meta, alerta al GameFlowManager
        if (ActividadCompletada)
            MarcarCompletada();
    }

    private void ActualizarUI()
    {
        if (contadorText != null)
            contadorText.text = $"{OrtigasRecolectadas}/{metaOrtigas}";
    }
}