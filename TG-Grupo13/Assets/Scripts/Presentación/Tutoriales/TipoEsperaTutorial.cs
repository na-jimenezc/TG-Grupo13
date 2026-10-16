/*Tipos de condición que puede tener un paso del tutorial para avanzar al siguiente
// Compartido entre las tres actividades (Deshierbe, Siembra, Corte)*/
public enum TipoEsperaTutorial
{
    //Base
    Tiempo,

    //ACTIVIDAD DE DESHIERBE
    EsperaAgarrarBolsa,
    EsperaAgarrarOrtiga,
    EsperaExtraerOrtiga,
    EsperaDepositarOrtiga,

    //ACTIVIDAD DE SIEMBRA
    EsperaAgarrarCanastaDosManos,
    EsperaInclinarCanasta,
    EsperaAgarrarPala,
    EsperaAgarrarBulbo,
    EsperaCavarSlot,
    EsperaSembrarBulbo,

    //ACTIVIDAD DE CORTE
    EsperaAgarrarTijeras,
    EsperaCortarTallo,
    EsperaDeshojarTallo,
    EsperaNivelarRamo,
    EsperaAgarrarCapuchon,
    EsperaEnsamblarRamo,
    EsperaDepositarRamoMalla,
    EsperaRecolectarRamo,
    EsperaDepositarBalde
}