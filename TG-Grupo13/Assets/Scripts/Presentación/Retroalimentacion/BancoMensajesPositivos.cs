using System;
using UnityEngine;

/*Banco de mensajes de retroalimentación positiva, editable desde el Inspector para que el experto en salud
 * ocupacional los valide sin tocar código (ISO 9241-171: lenguaje sencillo, positivo y específico).
 *
 * Para editarlo: clic derecho en Assets/Resources > Create > TG-Grupo13 > Banco de mensajes positivos,
 * y dejarlo con el nombre "BancoMensajesPositivos". Si no existe el asset, se usan los textos por defecto de abajo.
 *
 * Marcadores disponibles: {dominante} y {secundaria} (mano según el perfil), {actual} y {total} (solo en ramo listo).
 * IMPORTANTE: los recordatorios posturales son un BORRADOR y deben ser validados por el experto antes de las pruebas.*/
[CreateAssetMenu(fileName = "BancoMensajesPositivos", menuName = "TG-Grupo13/Banco de mensajes positivos")]
public class BancoMensajesPositivos : ScriptableObject
{
    public const string NOMBRE_RECURSO = "BancoMensajesPositivos";

    [Serializable]
    public class MensajesActividad
    {
        [Tooltip("Nivel 2: al llegar a la mitad de la meta")]
        public string mitad;
        [Tooltip("Nivel 3: al completar la actividad (título)")]
        public string completada;
        [Tooltip("Nivel 3: recordatorio postural en positivo que acompaña al logro")]
        [TextArea] public string recordatorioPostural;
    }

    [Header("Siembra")]
    public MensajesActividad siembra = new MensajesActividad
    {
        mitad = "¡Vas muy bien! Ya sembraste la mitad",
        completada = "¡Siembra completada! Lo hiciste muy bien",
        recordatorioPostural = "Recuerda: espalda recta y una rodilla apoyada en el piso"
    };

    [Header("Deshierbe")]
    public MensajesActividad deshierbe = new MensajesActividad
    {
        mitad = "¡Vas muy bien! Ya recogiste la mitad",
        completada = "¡Deshierbe completado! Excelente trabajo",
        recordatorioPostural = "Recuerda: apoya las rodillas y acércate a la maleza sin estirar la espalda"
    };

    [Header("Corte")]
    public MensajesActividad corte = new MensajesActividad
    {
        mitad = "¡Vas muy bien! Ya llevas la mitad de los ramos",
        completada = "¡Corte completado! Lo hiciste muy bien",
        recordatorioPostural = "Recuerda: acércate al tallo en vez de estirar el brazo y mantén la muñeca recta"
    };

    [Header("Corte: mensajes adicionales")]
    [Tooltip("Nivel 2: cada ramo terminado")]
    public string ramoListo = "¡Ramo listo! {actual} de {total}";
    [Tooltip("Nivel 2: cuando ya están todos los ramos y falta llevarlos al balde")]
    public string ramosListos = "¡Todos los ramos listos! Llévalos al balde";

    [Header("Sonido de logro (opcional)")]
    [Tooltip("Se reproduce en el nivel 3. Si se deja vacío, no suena nada extra")]
    public AudioClip sonidoLogro;

    public MensajesActividad Para(ActivityType? actividad)
    {
        switch (actividad)
        {
            case ActivityType.Siembra: return siembra;
            case ActivityType.Deshierbe: return deshierbe;
            case ActivityType.Corte: return corte;
            default: return null;
        }
    }

    //Carga el banco desde Resources; si no existe, crea uno en memoria con los textos por defecto
    public static BancoMensajesPositivos Cargar()
    {
        var banco = Resources.Load<BancoMensajesPositivos>(NOMBRE_RECURSO);
        return banco != null ? banco : CreateInstance<BancoMensajesPositivos>();
    }
}
