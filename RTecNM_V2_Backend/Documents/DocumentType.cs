namespace TecNM.Residency.Documents;

public static class DocumentType
{
    public const string Solicitud = "solicitud";
    public const string CartaPresentacion = "carta_presentacion";
    public const string CartaAceptacion = "carta_aceptacion";
    public const string Anteproyecto = "anteproyecto";
    public const string Dictamen = "dictamen";
    public const string ManualUsuario = "manual_usuario";
    public const string ManualTecnico = "manual_tecnico";
    public const string Libranza = "libranza";
    public const string Otro = "otro";

    public static readonly HashSet<string> ValidTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        Solicitud,
        CartaPresentacion,
        CartaAceptacion,
        Anteproyecto,
        Dictamen,
        ManualUsuario,
        ManualTecnico,
        Libranza,
        Otro
    };

    public static bool IsValid(string type) => ValidTypes.Contains(type);
}
