using TecNM.Residency.Common;

namespace TecNM.Residency.Companies;

public class CompanyService : ICompanyService
{
    private readonly ICompanyRepository _repository;

    public CompanyService(ICompanyRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<IEnumerable<CompanyResponseDto>>> GetAllAsync(bool activeOnly = false)
    {
        var companies = await _repository.GetAllAsync(activeOnly);
        var dtos = companies.Select(MapToResponseDto);
        return Result<IEnumerable<CompanyResponseDto>>.Success(dtos);
    }

    public async Task<Result<CompanyResponseDto>> GetByIdAsync(long id)
    {
        var company = await _repository.GetByIdAsync(id);
        if (company == null)
        {
            return Result<CompanyResponseDto>.Failure("Empresa no encontrada");
        }

        return Result<CompanyResponseDto>.Success(MapToResponseDto(company));
    }

    public async Task<Result<CompanyResponseDto>> CreateAsync(CreateCompanyDto dto, long? createdByUserId = null)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return Result<CompanyResponseDto>.Failure("El nombre de la empresa es obligatorio");
        }

        if (string.IsNullOrWhiteSpace(dto.Rfc))
        {
            return Result<CompanyResponseDto>.Failure("El RFC de la empresa es obligatorio");
        }

        var existingRfc = await _repository.GetByRfcAsync(dto.Rfc);
        if (existingRfc != null)
        {
            return Result<CompanyResponseDto>.Failure("Ya existe una empresa registrada con ese RFC");
        }

        var company = new Company
        {
            Name = dto.Name.Trim(),
            Rfc = dto.Rfc.Trim().ToUpperInvariant(),
            Sector = dto.Sector?.Trim(),
            Address = dto.Address?.Trim(),
            ContactName = dto.ContactName.Trim(),
            ContactEmail = dto.ContactEmail.Trim(),
            ContactPhone = dto.ContactPhone?.Trim(),
            IsActive = true,
            IsVisible = true,
            CreatedBy = createdByUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var created = await _repository.AddAsync(company);
        return Result<CompanyResponseDto>.Success(MapToResponseDto(created));
    }

    public async Task<Result<CompanyResponseDto>> UpdateAsync(long id, UpdateCompanyDto dto, long? updatedByUserId = null)
    {
        var company = await _repository.GetByIdAsync(id);
        if (company == null)
        {
            return Result<CompanyResponseDto>.Failure("Empresa no encontrada");
        }

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return Result<CompanyResponseDto>.Failure("El nombre de la empresa es obligatorio");
        }

        if (string.IsNullOrWhiteSpace(dto.Rfc))
        {
            return Result<CompanyResponseDto>.Failure("El RFC de la empresa es obligatorio");
        }

        var existingRfc = await _repository.GetByRfcAsync(dto.Rfc);
        if (existingRfc != null && existingRfc.Id != id)
        {
            return Result<CompanyResponseDto>.Failure("Ya existe otra empresa con ese RFC");
        }

        company.Name = dto.Name.Trim();
        company.Rfc = dto.Rfc.Trim().ToUpperInvariant();
        company.Sector = dto.Sector?.Trim();
        company.Address = dto.Address?.Trim();
        company.ContactName = dto.ContactName.Trim();
        company.ContactEmail = dto.ContactEmail.Trim();
        company.ContactPhone = dto.ContactPhone?.Trim();
        company.UpdatedBy = updatedByUserId;
        company.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(company);
        return Result<CompanyResponseDto>.Success(MapToResponseDto(company));
    }

    public async Task<Result<bool>> SoftDeleteAsync(long id, long deletedByUserId)
    {
        var company = await _repository.GetByIdAsync(id);
        if (company == null)
        {
            return Result<bool>.Failure("Empresa no encontrada");
        }

        company.IsActive = false;
        company.DeletedBy = deletedByUserId;
        company.DeletedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(company);
        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> ReactivateAsync(long id)
    {
        var company = await _repository.GetByIdAsync(id);
        if (company == null)
        {
            return Result<bool>.Failure("Empresa no encontrada");
        }

        company.IsActive = true;
        company.DeletedBy = null;
        company.DeletedAt = null;
        company.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(company);
        return Result<bool>.Success(true);
    }

    private static CompanyResponseDto MapToResponseDto(Company company) => new(
        company.Id,
        company.Name,
        company.Rfc,
        company.Sector,
        company.Address,
        company.ContactName,
        company.ContactEmail,
        company.ContactPhone,
        company.IsActive,
        company.IsVisible,
        company.DisplayOrder,
        company.CreatedBy,
        company.UpdatedBy,
        company.DeletedBy,
        company.DeletedAt,
        company.CreatedAt,
        company.UpdatedAt
    );
}
