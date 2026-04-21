using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using WebService_Agrometal_Variantes;

internal class App
{
    // Mismo nombre que usa el WebService MBOM (Web Service.exe).
    // Garantiza que MBOM y Contextos no corran al mismo tiempo.
    private const string MutexName = @"Global\DescarConector_WebService_SingleInstance";

    static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        // ── Mutex de exclusión mutua ────────────────────────────────────────────
        // Evita que el WebService de Contextos y el WebService de MBOM corran al mismo tiempo.
        // Si el otro conector está corriendo, espera hasta MutexTimeoutMinutes antes de abortar.
        const int MutexTimeoutMinutes = 480; // 8 horas

        using var mutex = new Mutex(initiallyOwned: false, name: MutexName, out _);
        bool acquired = false;
        try
        {
            Console.WriteLine($"Esperando turno (mutex)... timeout: {MutexTimeoutMinutes} min");
            acquired = mutex.WaitOne(TimeSpan.FromMinutes(MutexTimeoutMinutes));
        }
        catch (AbandonedMutexException)
        {
            // El proceso anterior terminó sin liberar el mutex; lo tomamos igual.
            acquired = true;
        }

        if (!acquired)
        {
            string msg = $"[{DateTime.Now:s}] ABORTADO: El otro WebService AgroConector no liberó el mutex en {MutexTimeoutMinutes} minutos.";
            Console.WriteLine(msg);
            try
            {
                Directory.CreateDirectory(@"C:\Temp");
                File.AppendAllText(@"C:\Temp\run.log", msg + Environment.NewLine);
            }
            catch { }
            return 1;
        }

        Console.WriteLine("Mutex adquirido. Iniciando procesamiento.");

        // ── Ruta del XML ────────────────────────────────────────────────────────
        // args[0] permite que el Monitor pase la ruta del XML directamente.
        // Si no se pasa argumento, usa la ruta por defecto (retrocompatibilidad).
        string xmlPath = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])
            ? args[0].Trim()
            : @"C:\temp\JsonContextos\ContextoADX.xml";

        string connectionString = "Server=SRV-TEAMCENTER;Database=CONTEXT_Agrometal;User Id=infodba;Password=infodba;TrustServerCertificate=True;";

        try
        {
            Directory.CreateDirectory(@"C:\Temp");
            File.AppendAllText(@"C:\Temp\run.log", DateTime.Now.ToString("s") + " - ARRANCA\n");
        }
        catch { }

        try
        {
            Console.WriteLine("INICIO");
            Console.WriteLine($"XML: {xmlPath}");

            if (!File.Exists(xmlPath))
                throw new FileNotFoundException("No existe el XML", xmlPath);

            Console.WriteLine("1) Cargando XML a SQL...");
            var processor = new XmlToSqlProcessor(connectionString);
            processor.Process(xmlPath);
            Console.WriteLine("1) OK - XML procesado.");

            string sqlConsulta = @"
SELECT
(SELECT Contexto.cfg0FamilyNamespace
 FROM Cfg0ProductModelFamily Familia
 INNER JOIN Cfg0ProdModelFamilyThread Contexto ON Familia.wso_thread = CONCAT('#',Contexto.GSIdentity_elemId)
) AS ID_Contexto,
Familia.cfg0ObjectId AS ID_Grupo,
Caracteristica.cfg0ObjectId AS ID_Caracteristica,
Familia.cfg0IsMultiselect AS SelMultiple,
Familia.cfg0IsDiscretionary AS Obligatorio
FROM Cfg0LiteralOptionValue Caracteristica
INNER JOIN Cfg0LiteralValueFamily Familia ON Familia.wso_thread = Caracteristica.cfg0OptionFamilyThread
";

            Console.WriteLine("2) Ejecutando consulta...");
            using var conn = new SqlConnection(connectionString);
            conn.Open();

            var contextos = ContextoBuilder.BuildFromQuery(conn, sqlConsulta);
            Console.WriteLine($"3) Contextos armados: {contextos.Count}");

            var api = new ProtheusContextosClient(
                baseUrlPost: "http://119.8.73.193:8086/rest/TCContextos/Incluir/",
                baseUrlPut: "http://119.8.73.193:8086/rest/TCContextos/Modificar/",
                user: "USERREST",
                pass: "restagr"
            );

            string carpetaJson = Path.Combine(Path.GetDirectoryName(xmlPath) ?? @"C:\Temp", "JsonContextos");

            foreach (var ctx in contextos)
            {
                GuardarJsonPorContexto(carpetaJson, ctx);

                try
                {
                    await api.SyncContextoAsync(ctx);
                    Console.WriteLine($"OK: {ctx.Id}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR API: {ctx.Id} - {ex.Message}");
                }
            }

            Console.WriteLine("FIN OK");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("FALLÓ:");
            Console.WriteLine(ex.ToString());
            return 1;
        }
    }

    static string SafeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name.Trim();
    }

    static void GuardarJsonPorContexto(string carpetaSalida, ContextoDto ctx)
    {
        Directory.CreateDirectory(carpetaSalida);

        var json = System.Text.Json.JsonSerializer.Serialize(ctx, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            WriteIndented = true
        });

        var filePath = Path.Combine(carpetaSalida, $"{SafeFileName(ctx.Id)}.txt");
        File.WriteAllText(filePath, json, Encoding.UTF8);
    }
}
