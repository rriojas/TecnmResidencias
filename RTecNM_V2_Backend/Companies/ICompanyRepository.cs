namespace TecNM.Residency.Companies;

public interface ICompanyRepository
{
    Task<IEnumerable<Company>> GetAllAsync(bool includeInactive = false);
    Task<Company?> GetByIdAsync(long id);
    Task<Company?> GetByRfcAsync(string rfc);
    Task<Company> AddAsync(Company company);
    Task UpdateAsync(Company company);
}
