using NUnit.Framework;

/*Fase 1: pruebas de lógica pura (no dependen de escenas ni de archivos).
 * Están en una carpeta Editor sin .asmdef para quedar en Assembly-CSharp-Editor, que sí ve los scripts del juego.
 * Se corren desde Window > General > Test Runner > EditMode.*/
public class PruebasValidacionEdad
{
    [Test]
    public void E1_EdadVacia_EsObligatoria()
    {
        Assert.IsFalse(ValidacionEdad.Validar("", out _, out string error));
        StringAssert.Contains("obligatoria", error);

        Assert.IsFalse(ValidacionEdad.Validar(null, out _, out _));
        Assert.IsFalse(ValidacionEdad.Validar("   ", out _, out _));
    }

    [Test]
    public void E1_EdadNoNumerica_EsRechazada()
    {
        Assert.IsFalse(ValidacionEdad.Validar("ab", out _, out string error));
        StringAssert.Contains("número", error);
    }

    [TestCase("17")]
    [TestCase("81")]
    [TestCase("0")]
    [TestCase("-20")]
    public void E1_EdadFueraDeRango_EsRechazada(string texto)
    {
        Assert.IsFalse(ValidacionEdad.Validar(texto, out _, out string error));
        StringAssert.Contains("entre", error);
    }

    [TestCase("18", "18")]
    [TestCase("80", "80")]
    [TestCase(" 25 ", "25")]
    [TestCase("045", "45")]
    public void E1_EdadEnRango_EsValidaYNormalizada(string texto, string esperada)
    {
        Assert.IsTrue(ValidacionEdad.Validar(texto, out string edad, out string error));
        Assert.AreEqual(esperada, edad);
        Assert.AreEqual("", error);
    }
}

public class PruebasLateralidad
{
    [TearDown]
    public void Limpiar() => Lateralidad.ForzadoEnEditor = null;

    [Test]
    public void L1_Diestro_FormateaManos()
    {
        Lateralidad.ForzadoEnEditor = false;
        Assert.AreEqual("Use la mano derecha y sostenga con la izquierda",
            Lateralidad.Formatear("Use la mano {dominante} y sostenga con la {secundaria}"));
    }

    [Test]
    public void L1_Zurdo_FormateaManos()
    {
        Lateralidad.ForzadoEnEditor = true;
        Assert.AreEqual("Use la mano izquierda y sostenga con la derecha",
            Lateralidad.Formatear("Use la mano {dominante} y sostenga con la {secundaria}"));
    }

    [Test]
    public void L1_TextoVacioONulo_NoFalla()
    {
        Assert.AreEqual("", Lateralidad.Formatear(""));
        Assert.IsNull(Lateralidad.Formatear(null));
    }

    [Test]
    public void L2_Diestro_ControlesCorrectos()
    {
        Lateralidad.ForzadoEnEditor = false;
        Assert.AreEqual(OVRInput.Controller.RTouch, Lateralidad.ControlDominante);
        Assert.AreEqual(OVRInput.Controller.LTouch, Lateralidad.ControlSecundario);
    }

    [Test]
    public void L2_Zurdo_ControlesCorrectos()
    {
        Lateralidad.ForzadoEnEditor = true;
        Assert.AreEqual(OVRInput.Controller.LTouch, Lateralidad.ControlDominante);
        Assert.AreEqual(OVRInput.Controller.RTouch, Lateralidad.ControlSecundario);
    }
}

public class PruebasAutorizacionAdmin
{
    [TearDown]
    public void Limpiar() => AutorizacionAdmin.Revocar();

    [Test]
    public void A1_Otorgar_QuedaVigente()
    {
        AutorizacionAdmin.Otorgar();
        Assert.IsTrue(AutorizacionAdmin.Vigente);
    }

    [Test]
    public void A1_Revocar_DejaDeEstarVigente()
    {
        AutorizacionAdmin.Otorgar();
        AutorizacionAdmin.Revocar();
        Assert.IsFalse(AutorizacionAdmin.Vigente);
    }
}
