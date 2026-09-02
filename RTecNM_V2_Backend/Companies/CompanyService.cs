using TecNM.Residency.Common;

namespace TecNM.Residency.Companies;

public class CompanyService : ICompanyService
{
    private readonly ICompanyRepository _repository;

    public CompanyService(ICompanyRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<IEnumerable<CompanyResponseDto>>> GetAllAsync(bool includeInactive = false)
    {
        var companies = await _repository.GetAllAsync(includeInactive);
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

    public async Task<Result<BatchImportResultDto>> ImportExcelAsync(Microsoft.AspNetCore.Http.IFormFile file, long? createdByUserId = null)
    {
        if (file == null || file.Length == 0)
        {
            return Result<BatchImportResultDto>.Failure("Debe seleccionar un archivo Excel válido.");
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".xlsx" && ext != ".xls")
        {
            return Result<BatchImportResultDto>.Failure("El archivo debe ser un documento Excel con extensión .xlsx o .xls.");
        }

        var expectedColumns = new List<string>
        {
            "Nombre", "RFC", "Sector", "Dirección", "NombreContacto", "CorreoContacto", "TeléfonoContacto"
        };

        using var stream = file.OpenReadStream();
        var (isValid, errorMessage, rows) = ExcelHelper.ParseExcelFile(stream, expectedColumns);

        if (!isValid)
        {
            return Result<BatchImportResultDto>.Failure(errorMessage ?? "Error de validación de encabezados en el archivo Excel.", 400);
        }

        var result = new BatchImportResultDto
        {
            TotalRows = rows.Count
        };

        int rowNum = 1;
        foreach (var row in rows)
        {
            rowNum++;
            var name = row.GetValueOrDefault("Nombre");
            var rfc = row.GetValueOrDefault("RFC");
            var sector = row.GetValueOrDefault("Sector");
            var address = row.GetValueOrDefault("Dirección");
            var contactName = row.GetValueOrDefault("NombreContacto");
            var contactEmail = row.GetValueOrDefault("CorreoContacto");
            var contactPhone = row.GetValueOrDefault("TeléfonoContacto");

            if (string.IsNullOrWhiteSpace(name))
            {
                result.ErrorCount++;
                result.Errors.Add($"Fila {rowNum}: El nombre de la empresa es obligatorio.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(rfc))
            {
                result.ErrorCount++;
                result.Errors.Add($"Fila {rowNum}: El RFC de la empresa es obligatorio.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(sector))
            {
                result.ErrorCount++;
                result.Errors.Add($"Fila {rowNum}: El sector de la empresa es obligatorio.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(address))
            {
                result.ErrorCount++;
                result.Errors.Add($"Fila {rowNum}: La dirección de la empresa es obligatoria.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(contactName))
            {
                result.ErrorCount++;
                result.Errors.Add($"Fila {rowNum}: El nombre de contacto es obligatorio.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(contactEmail))
            {
                result.ErrorCount++;
                result.Errors.Add($"Fila {rowNum}: El correo de contacto es obligatorio.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(contactPhone))
            {
                result.ErrorCount++;
                result.Errors.Add($"Fila {rowNum}: El teléfono de contacto es obligatorio.");
                continue;
            }

            var cleanRfc = rfc.Trim().ToUpperInvariant();
            if (cleanRfc.Length < 12 || cleanRfc.Length > 13)
            {
                result.ErrorCount++;
                result.Errors.Add($"Fila {rowNum}: El RFC '{cleanRfc}' debe contener 12 o 13 caracteres.");
                continue;
            }

            var cleanEmail = contactEmail.Trim().ToLowerInvariant();
            if (!cleanEmail.Contains('@') || !cleanEmail.Contains('.'))
            {
                result.ErrorCount++;
                result.Errors.Add($"Fila {rowNum}: El correo de contacto '{cleanEmail}' no es una dirección de correo válida.");
                continue;
            }

            var existing = await _repository.GetByRfcAsync(cleanRfc);
            if (existing != null)
            {
                result.SkippedCount++;
                result.Skipped.Add($"Fila {rowNum}: Omitida. Ya existe la empresa '{name}' con RFC '{cleanRfc}'.");
                continue;
            }

            var company = new Company
            {
                Name = name.Trim(),
                Rfc = cleanRfc,
                Sector = sector.Trim(),
                Address = address.Trim(),
                ContactName = contactName.Trim(),
                ContactEmail = cleanEmail,
                ContactPhone = contactPhone.Trim(),
                IsActive = true,
                IsVisible = true,
                CreatedBy = createdByUserId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _repository.AddAsync(company);
            result.SuccessCount++;
        }

        return Result<BatchImportResultDto>.Success(result);
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
