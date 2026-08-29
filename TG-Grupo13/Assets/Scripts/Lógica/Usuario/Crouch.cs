using UnityEngine;

/*Ayuda a ajustar de forma din´pamica el Character COntroller del jugador
 * según la altura de la cámara y la cabeza en el mundo real*/

public class CrouchAwareController : MonoBehaviour
{
    [SerializeField] private CharacterController playerController;
    [SerializeField] private Transform cameraTransform;      //Este es el transform de la cámara, que nos da la posición de la cabeza del jugador
    //Corresponde al Eye Center Anchor en Meta
    [SerializeField] private float minHeight = 0.4f;
    [SerializeField] private float maxHeight = 1.9f;
    [SerializeField] private float radius = 0.2f;

    void Update()
    {
        //Con Floor Level, la altura Y de la cámara YA es la altura real sobre el piso
        //Como está con position.y, si subes, el character controller también sube, y si agachas la cabeza, el character controller se agacha también
        float headHeight = cameraTransform.position.y;

        //Se mantiene la altura del Character Controller dentro de los límites establecidos
        float clampedHeight = Mathf.Clamp(headHeight, minHeight, maxHeight);

        //Reacomoda el tamaño del colisionador del Character Controller para que coincida con la altura de la cabeza del jugador
        playerController.height = clampedHeight;
        playerController.center = new Vector3(0f, clampedHeight / 2f, 0f);
        playerController.radius = radius;
    }
}