using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/*Fase 2: pruebas de la base de datos de usuarios (UserDatabase).
 * Cada prueba usa una carpeta temporal propia (UsarCarpetaParaPruebas), así nunca se toca el usuarios.json real.*/
public class PruebasUserDatabase
{
    private string _carpeta;

    private string RutaPrincipal => Path.Combine(_carpeta, "usuarios.json");
    private string RutaRespaldo => RutaPrincipal + ".bak";
    private string CarpetaRespaldos => Path.Combine(_carpeta, "Respaldos");

    [SetUp]
    public void Preparar()
    {
        _carpeta = Path.Combine(Path.GetTempPath(), "TG13_Pruebas_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_carpeta);
        UserDatabase.UsarCarpetaParaPruebas(_carpeta);
        AutorizacionAdmin.Revocar();
    }

    [TearDown]
    public void Limpiar()
    {
        UserDatabase.UsarCarpetaParaPruebas(null);
        AutorizacionAdmin.Revocar();
        if (Directory.Exists(_carpeta)) Directory.Delete(_carpeta, true);
    }

    //Olvida lo que hay en memoria para obligar a leer de nuevo desde el disco
    private void Recargar() => UserDatabase.UsarCarpetaParaPruebas(_carpeta);

    private static UserProfile CrearNormal(string nombre) => UserDatabase.CrearUsuario(nombre, "", false, null, "30");
    private static UserProfile CrearAdmin(string nombre, string clave = "clave") => UserDatabase.CrearUsuario(nombre, "", true, clave, "40");

    [Test]
    public void D1_CrearUsuario_IdsConsecutivosYHashSoloParaAdmin()
    {
        var ana = CrearNormal("Ana");
        var beto = CrearAdmin("Beto", "clave");

        Assert.AreEqual(1, ana.Id);
        Assert.AreEqual(2, beto.Id);
        Assert.AreEqual("", ana.PasswordHash);
        Assert.IsNotEmpty(beto.PasswordHash);
        Assert.AreNotEqual("clave", beto.PasswordHash, "La contraseña no debe guardarse en texto plano");
    }

    [Test]
    public void D2_BuscarUsuario_NoDistingueMayusculas()
    {
        CrearNormal("Ana");

        Assert.IsTrue(UserDatabase.ExisteUsuario("ANA"));
        Assert.AreEqual("Ana", UserDatabase.ObtenerUsuario("ana").Nombre);
        Assert.IsNull(UserDatabase.ObtenerUsuario("Nadie"));
    }

    [Test]
    public void D3_ValidarLoginAdmin()
    {
        CrearNormal("Ana");
        CrearAdmin("Beto", "clave");

        Assert.IsTrue(UserDatabase.ValidarLoginAdmin("Beto", "clave"));
        Assert.IsFalse(UserDatabase.ValidarLoginAdmin("Beto", "otra"));
        Assert.IsFalse(UserDatabase.ValidarLoginAdmin("Ana", ""), "Un usuario normal no puede entrar como admin");
        Assert.IsFalse(UserDatabase.ValidarLoginAdmin("Nadie", "clave"));
    }

    [Test]
    public void D4_NoSeEliminaAlUnicoAdministrador()
    {
        CrearAdmin("Beto");

        Assert.IsTrue(UserDatabase.EsUltimoAdministrador("Beto"));
        Assert.IsFalse(UserDatabase.EliminarUsuario("Beto"));
        Assert.IsTrue(UserDatabase.ExisteUsuario("Beto"));

        CrearAdmin("Caro");
        Assert.IsFalse(UserDatabase.EsUltimoAdministrador("Beto"));
        Assert.IsTrue(UserDatabase.EliminarUsuario("Beto"));
        Assert.IsFalse(UserDatabase.ExisteUsuario("Beto"));
    }

    [Test]
    public void D4_UsuarioNormal_SeEliminaSinRestriccion()
    {
        CrearNormal("Ana");

        Assert.IsFalse(UserDatabase.EsUltimoAdministrador("Ana"));
        Assert.IsTrue(UserDatabase.EliminarUsuario("Ana"));
        Assert.IsFalse(UserDatabase.ExisteUsuario("Ana"));
    }

    [Test]
    public void D5_LosDatosPersistenAlRecargar()
    {
        UserDatabase.CrearUsuario("Ana", "ana@correo.com", false, null, "25", esZurdo: true);

        Recargar();

        var ana = UserDatabase.ObtenerUsuario("Ana");
        Assert.IsNotNull(ana);
        Assert.AreEqual("25", ana.Edad);
        Assert.AreEqual("ana@correo.com", ana.Correo);
        Assert.IsTrue(ana.EsZurdo);
    }

    [Test]
    public void D6_ArchivoDanado_SeCargaElRespaldoYSeApartaElDanado()
    {
        CrearNormal("Ana");   //usuarios.json = {Ana}
        CrearNormal("Beto");  //usuarios.json = {Ana, Beto}; .bak = {Ana}
        Assert.IsTrue(File.Exists(RutaRespaldo), "El guardado debe dejar la versión anterior como .bak");

        File.WriteAllText(RutaPrincipal, "{esto no es json");
        Recargar();

        Assert.IsNotNull(UserDatabase.ObtenerUsuario("Ana"), "Debe cargar lo que había en el .bak");
        Assert.IsNull(UserDatabase.ObtenerUsuario("Beto"));
        Assert.AreEqual(1, Directory.GetFiles(_carpeta, "usuarios.corrupto-*.json").Length, "El archivo dañado se conserva aparte");
    }

    [Test]
    public void D6_ArchivoVacio_SeTrataComoDanado()
    {
        CrearNormal("Ana");
        CrearNormal("Beto");

        File.WriteAllText(RutaPrincipal, "");
        Recargar();

        Assert.IsNotNull(UserDatabase.ObtenerUsuario("Ana"));
    }

    [Test]
    public void D7_ArchivoYRespaldoDanados_ArrancaVacioSinExcepcion()
    {
        File.WriteAllText(RutaPrincipal, "{dañado");
        File.WriteAllText(RutaRespaldo, "{también dañado");
        LogAssert.Expect(LogType.Error, new Regex("dañados"));

        Recargar();

        Assert.AreEqual(0, UserDatabase.ObtenerTodosLosUsuarios().Count);
    }

    [Test]
    public void D8_RespaldoDiario_UnoPorDiaYSeConservan14()
    {
        CrearNormal("Ana"); //Primer guardado: aún no había archivo, no hay respaldo

        //20 respaldos viejos falsos
        Directory.CreateDirectory(CarpetaRespaldos);
        for (int dia = 1; dia <= 20; dia++)
            File.WriteAllText(Path.Combine(CarpetaRespaldos, $"usuarios-200101{dia:00}.json"), "{}");

        CrearNormal("Beto"); //Primer guardado del día con archivo existente: crea el respaldo de hoy y limpia
        CrearNormal("Caro"); //Segundo guardado del día: no crea otro

        string respaldoHoy = Path.Combine(CarpetaRespaldos, $"usuarios-{DateTime.Now:yyyyMMdd}.json");
        Assert.IsTrue(File.Exists(respaldoHoy));
        Assert.AreEqual(14, Directory.GetFiles(CarpetaRespaldos, "usuarios-*.json").Length);
        Assert.IsFalse(File.Exists(Path.Combine(CarpetaRespaldos, "usuarios-20010101.json")), "Se borran los más viejos");

        //El respaldo del día guarda cómo estaba ANTES de los cambios de hoy
        string contenido = File.ReadAllText(respaldoHoy);
        StringAssert.Contains("Ana", contenido);
        StringAssert.DoesNotContain("Beto", contenido);
    }

    [Test]
    public void D9_RespaldoManual_SeCreaYNoLoBorraLaLimpieza()
    {
        CrearNormal("Ana");

        string manual = UserDatabase.CrearRespaldoManual();
        Assert.IsNotNull(manual);
        Assert.IsTrue(File.Exists(manual));
        StringAssert.StartsWith("manual-usuarios-", Path.GetFileName(manual));

        for (int dia = 1; dia <= 20; dia++)
            File.WriteAllText(Path.Combine(CarpetaRespaldos, $"usuarios-200101{dia:00}.json"), "{}");
        File.Delete(Path.Combine(CarpetaRespaldos, $"usuarios-{DateTime.Now:yyyyMMdd}.json")); //Fuerza otra limpieza hoy
        CrearNormal("Beto");

        Assert.IsTrue(File.Exists(manual), "La limpieza de respaldos diarios no debe borrar los manuales");
    }

    [Test]
    public void A2_CrearAdministrador_SoloSinAdminsOConPase()
    {
        Assert.IsTrue(AutorizacionAdmin.PuedeCrearAdministrador(), "Sin administradores se permite crear el primero");

        CrearAdmin("Beto");
        Assert.IsFalse(AutorizacionAdmin.PuedeCrearAdministrador(), "Con un admin existente se necesita el pase");

        AutorizacionAdmin.Otorgar();
        Assert.IsTrue(AutorizacionAdmin.PuedeCrearAdministrador());
    }
}
