using UnityEngine;

public abstract class ActividadMenuUI : MonoBehaviour
{
    public static ActividadMenuUI ActivoActual { get; private set; }

    protected virtual void OnEnable()
    {
        ActivoActual = this;
    }

    protected virtual void OnDisable()
    {
        if (ActivoActual == this) ActivoActual = null;
    }

    public abstract string NombreActividad { get; } 
    public abstract void MostrarTutorial();
    public abstract void LanzarActividad();
}