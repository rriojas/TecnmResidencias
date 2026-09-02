using TecNM.Residency.Common;

namespace TecNM.Residency.Careers;

public class CareerService : ICareerService
{
    private readonly ICareerRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public CareerService(ICareerRepository repository, ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<Result<PaginatedResult<CareerResponseDto>>> GetPagedAsync(PaginationQuery query, string? status, bool includeInactive = false)
    {
        var paged = await _repository.GetPagedAsync(query, status, includeInactive);
        var dtos = paged.Items.Select(MapToResponseDto).ToList();

        var result = PaginatedResult<CareerResponseDto>.Create(
            dtos,
            paged.TotalCount,
            paged.PageNumber,
            paged.PageSize
        );

        return Result<PaginatedResult<CareerResponseDto>>.Success(result);
    }

    public async Task<Result<List<CareerResponseDto>>> GetAllAsync(bool includeInactive = false)
    {
        var careers = await _repository.GetAllAsync(includeInactive);
        var dtos = careers.Select(MapToResponseDto).ToList();
        return Result<List<CareerResponseDto>>.Success(dtos);
    }

    public async Task<Result<CareerResponseDto>> GetByIdAsync(long id)
    {
        var career = await _repository.GetByIdAsync(id);
        if (career is null)
            return Result<CareerResponseDto>.Failure("Carrera no encontrada.", 404);

        return Result<CareerResponseDto>.Success(MapToResponseDto(career));
    }

    public async Task<Result<CareerResponseDto>> CreateAsync(CreateCareerDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return Result<CareerResponseDto>.Failure("El nombre de la carrera es obligatorio.", 400);

        if (string.IsNullOrWhiteSpace(dto.Code))
            return Result<CareerResponseDto>.Failure("El código de la carrera es obligatorio.", 400);

        var existing = await _repository.GetByCodeAsync(dto.Code);
        if (existing is not null)
            return Result<CareerResponseDto>.Failure($"Ya existe una carrera registrada con el código '{dto.Code.Trim().ToUpper()}'.", 400);

        var career = new Career
        {
            Code = dto.Code.Trim().ToUpperInvariant(),
            Name = dto.Name.Trim(),
            Acronym = string.IsNullOrWhiteSpace(dto.Acronym) ? dto.Code.Trim().ToUpperInvariant() : dto.Acronym.Trim().ToUpperInvariant(),
            DepartmentId = dto.DepartmentId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId
        };

        var created = await _repository.CreateAsync(career);
        return Result<CareerResponseDto>.Success(MapToResponseDto(created));
    }

    public async Task<Result<CareerResponseDto>> UpdateAsync(long id, UpdateCareerDto dto)
    {
        var career = await _repository.GetByIdAsync(id);
        if (career is null)
            return Result<CareerResponseDto>.Failure("Carrera no encontrada.", 404);

        if (string.IsNullOrWhiteSpace(dto.Name))
            return Result<CareerResponseDto>.Failure("El nombre de la carrera es obligatorio.", 400);

        if (string.IsNullOrWhiteSpace(dto.Code))
            return Result<CareerResponseDto>.Failure("El código de la carrera es obligatorio.", 400);

        var cleanCode = dto.Code.Trim().ToUpperInvariant();
        if (career.Code.ToUpperInvariant() != cleanCode)
        {
            var existing = await _repository.GetByCodeAsync(cleanCode);
            if (existing is not null && existing.Id != id)
                return Result<CareerResponseDto>.Failure($"Ya existe otra carrera con el código '{cleanCode}'.", 400);
        }

        career.Code = cleanCode;
        career.Name = dto.Name.Trim();
        career.Acronym = string.IsNullOrWhiteSpace(dto.Acronym) ? cleanCode : dto.Acronym.Trim().ToUpperInvariant();
        career.DepartmentId = dto.DepartmentId;
        career.UpdatedAt = DateTime.UtcNow;
        career.UpdatedBy = _currentUser.UserId;

        var updated = await _repository.UpdateAsync(career);
        return Result<CareerResponseDto>.Success(MapToResponseDto(updated));
    }

    public async Task<Result<CareerResponseDto>> ToggleStatusAsync(long id)
    {
        var career = await _repository.GetByIdAsync(id);
        if (career is null)
            return Result<CareerResponseDto>.Failure("Carrera no encontrada.", 404);

        career.IsActive = !career.IsActive;
        career.UpdatedAt = DateTime.UtcNow;
        career.UpdatedBy = _currentUser.UserId;

        var updated = await _repository.UpdateAsync(career);
        return Result<CareerResponseDto>.Success(MapToResponseDto(updated));
    }

    private static CareerResponseDto MapToResponseDto(Career c)
    {
        return new CareerResponseDto(
            c.Id,
            c.Code,
            c.Name,
            c.Acronym,
            c.DepartmentId,
            c.IsActive,
            c.CreatedAt,
            c.UpdatedAt
        );
    }
}
