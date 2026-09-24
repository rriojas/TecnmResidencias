namespace TecNM.Residency.Documents;

public static class DocumentType
{
    public const string Solicitud = "solicitud";
    public const string CartaPresentacion = "carta_presentacion";
    public const string CartaAceptacion = "carta_aceptacion";
    public const string CartaAprobacion = "carta_aprobacion";
    public const string Anteproyecto = "anteproyecto";
    public const string Dictamen = "dictamen";
    public const string ManualUsuario = "manual_usuario";
    public const string ManualTecnico = "manual_tecnico";
    public const string Libranza = "libranza";
    public const string ConstanciaAcreditacion = "constancia_acreditacion";
    public const string Formato29 = "formato_29";
    public const string Formato29V2 = "formato_29v2";
    public const string Formato30 = "formato_30";
    public const string Otro = "otro";

    public static readonly HashSet<string> ValidTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        Solicitud,
        CartaAceptacion,
        CartaAprobacion,
        Dictamen,
        ManualUsuario,
        ManualTecnico,
        Libranza,
        ConstanciaAcreditacion,
        Formato29,
        Formato29V2,
        Formato30,
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
