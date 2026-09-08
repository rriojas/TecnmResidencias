namespace TecNM.Residency.Common;

public static class InstitutionalEmail
{
    public const string Domain = "@monclova.tecnm.mx";
    public const string ErrorMessage = "Debes ingresar un correo institucional válido (@monclova.tecnm.mx o @tecnm.mx).";

    public static List<string> AllowedDomains { get; set; } = new() { "@monclova.tecnm.mx", "@tecnm.mx" };

    public static bool IsValid(string? email)
    {
        var clean = StringSanitizer.SanitizeEmail(email);
        if (string.IsNullOrWhiteSpace(clean)) return false;

        return AllowedDomains.Any(d => clean.EndsWith(d.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase) && clean.Length > d.Length);
    }
}
