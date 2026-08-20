namespace TecNM.Residency.Common;

public static class InstitutionalEmail
{
    public const string Domain = "@monclova.tecnm.mx";
    public const string ErrorMessage = "Debes ingresar un correo institucional válido (@monclova.tecnm.mx).";

    public static bool IsValid(string? email)
    {
        var clean = (email ?? string.Empty).Trim().ToLowerInvariant();
        return clean.EndsWith(Domain, StringComparison.OrdinalIgnoreCase)
            && clean.Length > Domain.Length;
    }
}
