using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/*El panel del Game Master (pantalla del PC) se maneja solo con el mouse.
 * El EventSystem de Core usa las acciones por defecto de Unity, donde el gatillo de los controles del visor
 * (<XRController>/trigger) también cuenta como "clic", y "Submit" (Enter, Espacio, mando) presiona el botón que
 * haya quedado seleccionado. Cada vez que el participante apretaba el gatillo (p. ej. para cavar en Siembra), se
 * presionaba el botón del panel: "Ir al tutorial", luego "Saltar a la actividad", y la sesión avanzaba sola.
 * Aquí:
 *  - el clic de la UI queda solo para el botón izquierdo del mouse;
 *  - "Submit" no presiona botones (queda sin teclas);
 *  - ningún botón queda seleccionado después de usarlo (los campos de texto sí, para poder escribir).
 * Los menús dentro del visor no se afectan: usan los rayos de Meta (Interaction SDK), no este módulo.
 * Se aplica solo al iniciar, sin modificar la escena.*/
public class ClicSoloConMouse : MonoBehaviour
{
    private static InputAction _clic, _sinSubmit;
    private static ClicSoloConMouse _instancia;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Iniciar()
    {
        if (_instancia == null)
        {
            var go = new GameObject("ClicSoloConMouse");
            DontDestroyOnLoad(go);
            _instancia = go.AddComponent<ClicSoloConMouse>();
        }
        AplicarATodos();
        SceneManager.sceneLoaded -= AlCargarEscena;
        SceneManager.sceneLoaded += AlCargarEscena;
    }

    private static void AlCargarEscena(Scene escena, LoadSceneMode modo) => AplicarATodos();

    private static void AplicarATodos()
    {
        if (_clic == null)
        {
            //El Input System solo deja crear referencias (InputActionReference) a acciones que estén dentro de un
            //InputActionAsset: sin esto fallaba al iniciar y el gatillo de los controles volvía a contar como clic.
            var acciones = ScriptableObject.CreateInstance<InputActionAsset>();
            acciones.name = "ClicSoloConMouse";
            var mapa = acciones.AddActionMap("PanelGameMaster");
            _clic = mapa.AddAction("ClicSoloMouse", InputActionType.PassThrough, "<Mouse>/leftButton", expectedControlLayout: "Button");
            _sinSubmit = mapa.AddAction("SubmitDesactivado", InputActionType.Button);
            acciones.Enable();
        }

        int n = 0;
        foreach (var modulo in Object.FindObjectsByType<InputSystemUIInputModule>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (modulo.leftClick == null || modulo.leftClick.action != _clic) modulo.leftClick = InputActionReference.Create(_clic);
            if (modulo.submit == null || modulo.submit.action != _sinSubmit) modulo.submit = InputActionReference.Create(_sinSubmit);
            n++;
        }
        Debug.Log($"[UI] Panel del Game Master solo con el mouse ({n} módulo(s) de UI).");
    }

    //Que ningún botón quede seleccionado: así nada lo puede "presionar" después sin el mouse.
    private void LateUpdate()
    {
        var sistema = EventSystem.current;
        if (sistema == null) return;
        var seleccionado = sistema.currentSelectedGameObject;
        if (seleccionado != null && seleccionado.GetComponent<Button>() != null) sistema.SetSelectedGameObject(null);
    }
}
