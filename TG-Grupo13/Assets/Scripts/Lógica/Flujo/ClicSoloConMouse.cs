using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

/*El panel del Game Master (pantalla del PC) se maneja solo con el mouse.
 * El EventSystem de Core usa las acciones por defecto de Unity, donde el gatillo de los controles del visor
 * (<XRController>/trigger) también cuenta como "clic". Cada vez que el participante apretaba el gatillo (p. ej. para
 * cavar en Siembra), se hacía clic en el botón del panel que estuviera bajo el puntero del mouse: después de presionar
 * "Comenzar", el puntero quedaba sobre el mismo botón, que pasa a decir "Ir al tutorial" y luego "Saltar a la
 * actividad", y la sesión avanzaba sola de pre a tutorial y a post.
 * Aquí el clic de la UI queda solo para el mouse. Los menús dentro del visor no se afectan: usan los rayos de
 * Meta (Interaction SDK), no este módulo. Se aplica solo al iniciar, sin modificar la escena.*/
public static class ClicSoloConMouse
{
    private static InputAction _clic;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Iniciar()
    {
        AplicarATodos();
        SceneManager.sceneLoaded -= AlCargarEscena;
        SceneManager.sceneLoaded += AlCargarEscena;
    }

    private static void AlCargarEscena(Scene escena, LoadSceneMode modo) => AplicarATodos();

    private static void AplicarATodos()
    {
        if (_clic == null)
        {
            _clic = new InputAction("ClicSoloMouse", InputActionType.PassThrough, "<Mouse>/leftButton", expectedControlType: "Button");
            _clic.Enable();
        }

        foreach (var modulo in Object.FindObjectsByType<InputSystemUIInputModule>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (modulo.leftClick != null && modulo.leftClick.action == _clic) continue;
            modulo.leftClick = InputActionReference.Create(_clic);
        }
    }
}
