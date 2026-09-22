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
    public const string ConstanciaAcreditacion = "constancia_acreditacion";
    public const string Otro = "otro";

    public static readonly HashSet<string> ValidTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        Solicitud,
        CartaAceptacion,
        Dictamen,
        ManualUsuario,
        ManualTecnico,
        Libranza,
        ConstanciaAcreditacion,
        Otro
    };

    public static readonly HashSet<string> OmittedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        Anteproyecto,
        CartaPresentacion
    };

    public static bool IsValid(string type) => ValidTypes.Contains(type);
    public static bool IsOmitted(string type) => OmittedTypes.Contains(type);
}
