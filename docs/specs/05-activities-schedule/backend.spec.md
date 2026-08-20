# 05 - Activities Schedule Domain Specification (Backend C# .NET 10 + EF Core 10)

Module: `src/Activities/`  
Domain: 26-Week Activity Planning & Progress Tracking  
Tech Stack: **C# .NET 10** (ASP.NET Core Web API), **Entity Framework Core 10**, **PostgreSQL 18**

---

## 1. Colocated Files (Vertical Slice C#)

- `src/Activities/ActivitiesController.cs`
- `src/Activities/ActivityService.cs`
- `src/Activities/IActivityRepository.cs`
- `src/Activities/ActivityRepository.cs`
- `src/Activities/WeeklyActivityDto.cs`
- `src/Activities/WeeklyActivity.cs`
- `src/Activities/WeeklyProgress.cs`

---

## 2. API Endpoints

### 2.1 Get Project Schedule (26 Weeks)
- **Endpoint**: `GET /api/v1/projects/{projectId}/activities`

### 2.2 Save Weekly Progress
- **Endpoint**: `POST /api/v1/projects/{projectId}/activities/progress`
- **Request Body**:
  ```json
  {
    "activityId": 12,
    "weekNumber": 5,
    "status": "completed"
  }
  ```

---

## 3. Business Rules & EF Core PostgreSQL 18 Mapping

1. **Unpivoted Architecture**: Weeks 1-26 are stored as normalized rows in PostgreSQL 18 `weekly_progress` table (`week_number INTEGER`, `status VARCHAR(20)`).
2. **Database Schema**:
   - `weekly_activities`: `id` (`BIGSERIAL`, PK), `project_id` (`BIGINT`, FK), `activity_description` (`TEXT`, NOT NULL).
   - `weekly_progress`: `id` (`BIGSERIAL`, PK), `activity_id` (`BIGINT`, FK), `week_number` (`INT`, CHECK 1..26), `status` (`VARCHAR(20)`).
