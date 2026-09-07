using TecNM.Residency.Common;

namespace TecNM.Residency.Companies;

public class CompanyService : ICompanyService
{
    private readonly ICompanyRepository _repository;

    public CompanyService(ICompanyRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<PaginatedResult<CompanyResponseDto>>> GetPagedAsync(PaginationQuery query, string? status, bool includeInactive = false)
    {
        var paged = await _repository.GetPagedAsync(query, status, includeInactive);
        var dtos = paged.Items.Select(MapToResponseDto).ToList();

        var result = PaginatedResult<CompanyResponseDto>.Create(
            dtos,
            paged.TotalCount,
            paged.PageNumber,
            paged.PageSize
        );

        return Result<PaginatedResult<CompanyResponseDto>>.Success(result);
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
        var primaryName = !string.IsNullOrWhiteSpace(dto.Name) 
            ? dto.Name.Trim() 
            : (!string.IsNullOrWhiteSpace(dto.LegalName) ? dto.LegalName.Trim() : dto.TradeName?.Trim());

        if (string.IsNullOrWhiteSpace(primaryName))
        {
            return Result<CompanyResponseDto>.Failure("El nombre o razón social de la empresa es obligatorio");
        }

        string? cleanRfc = !string.IsNullOrWhiteSpace(dto.Rfc) ? dto.Rfc.Trim().ToUpperInvariant() : null;
        if (cleanRfc != null)
        {
            var existingRfc = await _repository.GetByRfcAsync(cleanRfc);
            if (existingRfc != null)
            {
                return Result<CompanyResponseDto>.Failure("Ya existe una empresa registrada con ese RFC");
            }
        }

        var street = dto.Street?.Trim();
        var number = dto.Number?.Trim();
        var colonia = dto.Colonia?.Trim();
        var city = dto.City?.Trim();
        var state = dto.State?.Trim();
        var postalCode = dto.PostalCode?.Trim();

        string? compositeAddress = dto.Address?.Trim();
        if (string.IsNullOrWhiteSpace(compositeAddress) && (!string.IsNullOrWhiteSpace(street) || !string.IsNullOrWhiteSpace(city)))
        {
            compositeAddress = $"{street} #{number}, Col. {colonia}, {city}, {state}, C.P. {postalCode}".Trim();
        }

        var company = new Company
        {
            Name = primaryName,
            LegalName = !string.IsNullOrWhiteSpace(dto.LegalName) ? dto.LegalName.Trim() : null,
            TradeName = !string.IsNullOrWhiteSpace(dto.TradeName) ? dto.TradeName.Trim() : null,
            Rfc = cleanRfc,
            Sector = dto.Sector?.Trim(),
            Address = compositeAddress,
            Street = street,
            Number = number,
            Colonia = colonia,
            City = city,
            State = state,
            PostalCode = postalCode,
            ContactName = dto.ContactName.Trim(),
            ContactEmail = dto.ContactEmail.Trim(),
            ContactPhone = dto.ContactPhone?.Trim(),
            HasAgreement = false,
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

        var primaryName = !string.IsNullOrWhiteSpace(dto.Name) 
            ? dto.Name.Trim() 
            : (!string.IsNullOrWhiteSpace(dto.LegalName) ? dto.LegalName.Trim() : dto.TradeName?.Trim());

        if (string.IsNullOrWhiteSpace(primaryName))
        {
            return Result<CompanyResponseDto>.Failure("El nombre o razón social de la empresa es obligatorio");
        }

        string? cleanRfc = !string.IsNullOrWhiteSpace(dto.Rfc) ? dto.Rfc.Trim().ToUpperInvariant() : null;
        if (cleanRfc != null)
        {
            var existingRfc = await _repository.GetByRfcAsync(cleanRfc);
            if (existingRfc != null && existingRfc.Id != id)
            {
                return Result<CompanyResponseDto>.Failure("Ya existe otra empresa con ese RFC");
            }
        }

        var street = dto.Street?.Trim();
        var number = dto.Number?.Trim();
        var colonia = dto.Colonia?.Trim();
        var city = dto.City?.Trim();
        var state = dto.State?.Trim();
        var postalCode = dto.PostalCode?.Trim();

        string? compositeAddress = dto.Address?.Trim();
        if (!string.IsNullOrWhiteSpace(street) || !string.IsNullOrWhiteSpace(city))
        {
            compositeAddress = $"{street} #{number}, Col. {colonia}, {city}, {state}, C.P. {postalCode}".Trim();
        }

        company.Name = primaryName;
        company.LegalName = !string.IsNullOrWhiteSpace(dto.LegalName) ? dto.LegalName.Trim() : null;
        company.TradeName = !string.IsNullOrWhiteSpace(dto.TradeName) ? dto.TradeName.Trim() : null;
        company.Rfc = cleanRfc;
        company.Sector = dto.Sector?.Trim();
        company.Address = compositeAddress ?? company.Address;
        company.Street = street ?? company.Street;
        company.Number = number ?? company.Number;
        company.Colonia = colonia ?? company.Colonia;
        company.City = city ?? company.City;
        company.State = state ?? company.State;
        company.PostalCode = postalCode ?? company.PostalCode;
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
            "Nombre", "RazonSocial", "NombreComercial", "RFC", "Sector", "Calle", "Numero", "Colonia", "Ciudad", "Estado", "CodigoPostal", "NombreContacto", "CorreoContacto", "TeléfonoContacto"
        };

        using var stream = file.OpenReadStream();
        var (isValid, errorMessage, rows) = ExcelHelper.ParseExcelFile(stream, expectedColumns);

        // Fallback to allow old template headers if someone uploads old template
        if (!isValid)
        {
            stream.Position = 0;
            var oldColumns = new List<string> { "Nombre", "RFC", "Sector", "Dirección", "NombreContacto", "CorreoContacto", "TeléfonoContacto" };
            var oldParse = ExcelHelper.ParseExcelFile(stream, oldColumns);
            if (oldParse.IsValid)
            {
                isValid = true;
                rows = oldParse.Rows;
            }
        }

        if (!isValid)
        {
            return Result<BatchImportResultDto>.Failure(errorMessage ?? "Error de validación de encabezados en el archivo Excel.", 400);
        }

        // FASE 1: VALIDACIÓN TOTAL ESTRICTA. Si falta algún dato requerido en cualquier fila, abortar todo.
        int checkRowNum = 1;
        foreach (var row in rows)
        {
            checkRowNum++;
            var name = row.GetValueOrDefault("Nombre")?.Trim();
            var legalName = row.GetValueOrDefault("RazonSocial")?.Trim();
            var sector = row.GetValueOrDefault("Sector")?.Trim();
            var contactName = row.GetValueOrDefault("NombreContacto")?.Trim();
            var contactEmail = row.GetValueOrDefault("CorreoContacto")?.Trim();
            var contactPhone = (row.GetValueOrDefault("TeléfonoContacto") ?? row.GetValueOrDefault("TelefonoContacto"))?.Trim();

            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(legalName))
            {
                return Result<BatchImportResultDto>.Failure($"Fila {checkRowNum}: El Nombre o Razón Social es obligatorio. Proceso detenido para evitar datos incompletos.", 400);
            }

            if (string.IsNullOrWhiteSpace(sector))
            {
                return Result<BatchImportResultDto>.Failure($"Fila {checkRowNum}: El Sector es obligatorio. Proceso detenido.", 400);
            }

            if (string.IsNullOrWhiteSpace(contactName))
            {
                return Result<BatchImportResultDto>.Failure($"Fila {checkRowNum}: El Nombre de Contacto es obligatorio. Proceso detenido.", 400);
            }

            if (string.IsNullOrWhiteSpace(contactEmail))
            {
                return Result<BatchImportResultDto>.Failure($"Fila {checkRowNum}: El Correo de Contacto es obligatorio. Proceso detenido.", 400);
            }

            if (!contactEmail.Contains('@') || !contactEmail.Contains('.'))
            {
                return Result<BatchImportResultDto>.Failure($"Fila {checkRowNum}: El Correo '{contactEmail}' no es válido. Proceso detenido.", 400);
            }

            if (string.IsNullOrWhiteSpace(contactPhone))
            {
                return Result<BatchImportResultDto>.Failure($"Fila {checkRowNum}: El Teléfono de Contacto es obligatorio. Proceso detenido.", 400);
            }
        }

        // FASE 2: PROCESAR REGISTROS. Si la empresa ya existe, solo completar campos que estén NULL en la base de datos.
        var result = new BatchImportResultDto
        {
            TotalRows = rows.Count
        };

        int rowNum = 1;
        foreach (var row in rows)
        {
            rowNum++;
            var name = row.GetValueOrDefault("Nombre")?.Trim();
            var legalName = row.GetValueOrDefault("RazonSocial")?.Trim();
            var tradeName = row.GetValueOrDefault("NombreComercial")?.Trim();
            var rfc = row.GetValueOrDefault("RFC")?.Trim();
            var sector = row.GetValueOrDefault("Sector")?.Trim();
            var calle = row.GetValueOrDefault("Calle")?.Trim();
            var numero = row.GetValueOrDefault("Numero")?.Trim();
            var colonia = row.GetValueOrDefault("Colonia")?.Trim();
            var ciudad = row.GetValueOrDefault("Ciudad")?.Trim();
            var estado = row.GetValueOrDefault("Estado")?.Trim();
            var codigoPostal = row.GetValueOrDefault("CodigoPostal")?.Trim();
            var oldAddress = row.GetValueOrDefault("Dirección")?.Trim();
            var contactName = row.GetValueOrDefault("NombreContacto")?.Trim();
            var contactEmail = row.GetValueOrDefault("CorreoContacto")?.Trim().ToLowerInvariant();
            var contactPhone = (row.GetValueOrDefault("TeléfonoContacto") ?? row.GetValueOrDefault("TelefonoContacto"))?.Trim();

            var primaryName = !string.IsNullOrWhiteSpace(name) ? name : (legalName ?? tradeName ?? "Empresa");
            string? cleanRfc = !string.IsNullOrWhiteSpace(rfc) ? rfc.ToUpperInvariant() : null;

            // Buscar si ya existe la empresa
            Company? existing = null;
            if (cleanRfc != null)
            {
                existing = await _repository.GetByRfcAsync(cleanRfc);
            }
            if (existing == null && !string.IsNullOrWhiteSpace(primaryName))
            {
                existing = await _repository.GetByNameOrLegalNameAsync(primaryName);
            }

            if (existing != null)
            {
                // Solo modificar columnas que NO contengan datos previamente
                bool modified = false;
                if (string.IsNullOrWhiteSpace(existing.LegalName) && !string.IsNullOrWhiteSpace(legalName))
                {
                    existing.LegalName = legalName;
                    modified = true;
                }
                if (string.IsNullOrWhiteSpace(existing.TradeName) && !string.IsNullOrWhiteSpace(tradeName))
                {
                    existing.TradeName = tradeName;
                    modified = true;
                }
                if (string.IsNullOrWhiteSpace(existing.Rfc) && cleanRfc != null)
                {
                    existing.Rfc = cleanRfc;
                    modified = true;
                }
                if (string.IsNullOrWhiteSpace(existing.Sector) && !string.IsNullOrWhiteSpace(sector))
                {
                    existing.Sector = sector;
                    modified = true;
                }
                if (string.IsNullOrWhiteSpace(existing.Street) && !string.IsNullOrWhiteSpace(calle))
                {
                    existing.Street = calle;
                    modified = true;
                }
                if (string.IsNullOrWhiteSpace(existing.Number) && !string.IsNullOrWhiteSpace(numero))
                {
                    existing.Number = numero;
                    modified = true;
                }
                if (string.IsNullOrWhiteSpace(existing.Colonia) && !string.IsNullOrWhiteSpace(colonia))
                {
                    existing.Colonia = colonia;
                    modified = true;
                }
                if (string.IsNullOrWhiteSpace(existing.City) && !string.IsNullOrWhiteSpace(ciudad))
                {
                    existing.City = ciudad;
                    modified = true;
                }
                if (string.IsNullOrWhiteSpace(existing.State) && !string.IsNullOrWhiteSpace(estado))
                {
                    existing.State = estado;
                    modified = true;
                }
                if (string.IsNullOrWhiteSpace(existing.PostalCode) && !string.IsNullOrWhiteSpace(codigoPostal))
                {
                    existing.PostalCode = codigoPostal;
                    modified = true;
                }
                if (string.IsNullOrWhiteSpace(existing.Address))
                {
                    if (!string.IsNullOrWhiteSpace(oldAddress))
                    {
                        existing.Address = oldAddress;
                        modified = true;
                    }
                    else if (!string.IsNullOrWhiteSpace(existing.Street) || !string.IsNullOrWhiteSpace(existing.City))
                    {
                        existing.Address = $"{existing.Street} #{existing.Number}, Col. {existing.Colonia}, {existing.City}, {existing.State}, C.P. {existing.PostalCode}".Trim();
                        modified = true;
                    }
                }
                if (string.IsNullOrWhiteSpace(existing.ContactPhone) && !string.IsNullOrWhiteSpace(contactPhone))
                {
                    existing.ContactPhone = contactPhone;
                    modified = true;
                }

                if (modified)
                {
                    existing.UpdatedAt = DateTime.UtcNow;
                    existing.UpdatedBy = createdByUserId;
                    await _repository.UpdateAsync(existing);
                    result.SuccessCount++;
                }
                else
                {
                    result.SkippedCount++;
                    result.Skipped.Add($"Fila {rowNum}: Omitida. La empresa '{existing.Name}' ya cuenta con todos los datos registrados.");
                }
            }
            else
            {
                string? compositeAddress = oldAddress;
                if (string.IsNullOrWhiteSpace(compositeAddress) && (!string.IsNullOrWhiteSpace(calle) || !string.IsNullOrWhiteSpace(ciudad)))
                {
                    compositeAddress = $"{calle} #{numero}, Col. {colonia}, {ciudad}, {estado}, C.P. {codigoPostal}".Trim();
                }

                var newCompany = new Company
                {
                    Name = primaryName,
                    LegalName = !string.IsNullOrWhiteSpace(legalName) ? legalName : null,
                    TradeName = !string.IsNullOrWhiteSpace(tradeName) ? tradeName : null,
                    Rfc = cleanRfc,
                    Sector = sector,
                    Address = compositeAddress,
                    Street = calle,
                    Number = numero,
                    Colonia = colonia,
                    City = ciudad,
                    State = estado,
                    PostalCode = codigoPostal,
                    ContactName = contactName!,
                    ContactEmail = contactEmail!,
                    ContactPhone = contactPhone,
                    HasAgreement = false,
                    IsActive = true,
                    IsVisible = true,
                    CreatedBy = createdByUserId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _repository.AddAsync(newCompany);
                result.SuccessCount++;
            }
        }

        return Result<BatchImportResultDto>.Success(result);
    }

    public async Task<Result<CompanyAgreementDto>> GetAgreementByIdAsync(long agreementId)
    {
        var agreement = await _repository.GetAgreementByIdAsync(agreementId);
        if (agreement == null)
        {
            return Result<CompanyAgreementDto>.Failure("Convenio no encontrado.", 404);
        }

        return Result<CompanyAgreementDto>.Success(MapToAgreementDto(agreement));
    }

    public async Task<Result<List<CompanyAgreementDto>>> GetAgreementsByCompanyIdAsync(long companyId)
    {
        var agreements = await _repository.GetAgreementsByCompanyIdAsync(companyId);
        var dtos = agreements.Select(MapToAgreementDto).ToList();
        return Result<List<CompanyAgreementDto>>.Success(dtos);
    }

    public async Task<Result<PaginatedResult<CompanyAgreementDto>>> GetAgreementsPagedAsync(PaginationQuery query, string? statusFilter)
    {
        var paged = await _repository.GetAgreementsPagedAsync(query, statusFilter);
        var dtos = paged.Items.Select(MapToAgreementDto).ToList();

        var result = PaginatedResult<CompanyAgreementDto>.Create(
            dtos,
            paged.TotalCount,
            paged.PageNumber,
            paged.PageSize
        );

        return Result<PaginatedResult<CompanyAgreementDto>>.Success(result);
    }

    public async Task<Result<CompanyAgreementDto>> CreateAgreementAsync(SaveCompanyAgreementDto dto, long? userId = null)
    {
        var agreement = new CompanyAgreement
        {
            ArchiveId = dto.ArchiveId?.Trim(),
            Status = !string.IsNullOrWhiteSpace(dto.Status) ? dto.Status.Trim().ToUpperInvariant() : "1 VIGENTE",
            PitCode = dto.PitCode?.Trim(),
            CiaType = dto.CiaType?.Trim(),
            AgreementScope = dto.AgreementScope?.Trim(),
            Sector = dto.Sector?.Trim(),
            BusinessLine = dto.BusinessLine?.Trim(),
            CompanySize = dto.CompanySize?.Trim(),
            GeographicScope = dto.GeographicScope?.Trim(),
            Notes = dto.Notes?.Trim(),
            IsActive = true,
            IsVisible = true,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var companyIds = dto.CompanyIds ?? new List<long>();
        var created = await _repository.AddAgreementAsync(agreement, companyIds);
        var reloaded = await _repository.GetAgreementByIdAsync(created.Id);

        return Result<CompanyAgreementDto>.Success(MapToAgreementDto(reloaded ?? created));
    }

    public async Task<Result<CompanyAgreementDto>> UpdateAgreementAsync(long agreementId, SaveCompanyAgreementDto dto, long? userId = null)
    {
        var agreement = await _repository.GetAgreementByIdAsync(agreementId);
        if (agreement == null)
        {
            return Result<CompanyAgreementDto>.Failure("Convenio no encontrado.", 404);
        }

        agreement.ArchiveId = dto.ArchiveId?.Trim();
        agreement.Status = !string.IsNullOrWhiteSpace(dto.Status) ? dto.Status.Trim().ToUpperInvariant() : "1 VIGENTE";
        agreement.PitCode = dto.PitCode?.Trim();
        agreement.CiaType = dto.CiaType?.Trim();
        agreement.AgreementScope = dto.AgreementScope?.Trim();
        agreement.Sector = dto.Sector?.Trim();
        agreement.BusinessLine = dto.BusinessLine?.Trim();
        agreement.CompanySize = dto.CompanySize?.Trim();
        agreement.GeographicScope = dto.GeographicScope?.Trim();
        agreement.Notes = dto.Notes?.Trim();
        agreement.UpdatedBy = userId;
        agreement.UpdatedAt = DateTime.UtcNow;

        var companyIds = dto.CompanyIds ?? new List<long>();
        await _repository.UpdateAgreementAsync(agreement, companyIds);
        var reloaded = await _repository.GetAgreementByIdAsync(agreementId);

        return Result<CompanyAgreementDto>.Success(MapToAgreementDto(reloaded ?? agreement));
    }

    public async Task<Result<bool>> DeleteAgreementAsync(long agreementId)
    {
        var agreement = await _repository.GetAgreementByIdAsync(agreementId);
        if (agreement == null)
        {
            return Result<bool>.Failure("Convenio no encontrado.", 404);
        }

        await _repository.DeleteAgreementAsync(agreementId);
        return Result<bool>.Success(true);
    }

    private static CompanyResponseDto MapToResponseDto(Company company) => new(
        company.Id,
        company.Name,
        company.LegalName,
        company.TradeName,
        company.Rfc,
        company.Sector,
        company.Address,
        company.Street,
        company.Number,
        company.Colonia,
        company.City,
        company.State,
        company.PostalCode,
        company.ContactName,
        company.ContactEmail,
        company.ContactPhone,
        company.HasAgreement || (company.AgreementCompanies != null && company.AgreementCompanies.Any()),
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

    private static CompanyAgreementDto MapToAgreementDto(CompanyAgreement a) => new(
        a.Id,
        a.ArchiveId,
        a.Status,
        a.PitCode,
        a.CiaType,
        a.AgreementScope,
        a.Sector,
        a.BusinessLine,
        a.CompanySize,
        a.GeographicScope,
        a.Notes,
        a.AgreementCompanies != null
            ? a.AgreementCompanies
                .Where(ac => ac.Company != null)
                .Select(ac => new CompanyBriefDto(
                    ac.Company!.Id,
                    ac.Company.Name,
                    ac.Company.LegalName,
                    ac.Company.TradeName,
                    ac.Company.Rfc))
                .ToList()
            : new List<CompanyBriefDto>(),
        a.IsActive,
        a.CreatedAt,
        a.UpdatedAt
    );
}
