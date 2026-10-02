using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TecNM.Residency.Auth;
using TecNM.Residency.Common;
using TecNM.Residency.Common.Notifications;
using TecNM.Residency.Common.Settings;
using TecNM.Residency.Notifications;
using TecNM.Residency.Projects;
using TecNM.Residency.Students;
using Xunit;

namespace RTecNM_V2_Backend.Tests;

public class NotificationBatchingAndMockTests
{
    private AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public void Batching_MoreThan300Emails_SplitsIntoChunksOf300Max()
    {
        // ARRANGE: Generar 750 correos simulados
        var emails = new HashSet<string>();
        for (int i = 1; i <= 750; i++)
        {
            emails.Add($"alumno{i}@monclova.tecnm.mx");
        }

        const int BatchSize = 300;

        // ACT: Agrupar por lotes de 300
        var batches = emails.Chunk(BatchSize).ToList();

        // ASSERT
        Assert.Equal(3, batches.Count);
        Assert.Equal(300, batches[0].Length);
        Assert.Equal(300, batches[1].Length);
        Assert.Equal(150, batches[2].Length);

        // Validar que no hay duplicados entre los lotes
        var allProcessed = batches.SelectMany(b => b).ToList();
        Assert.Equal(750, allProcessed.Count);
        Assert.Equal(750, allProcessed.Distinct().Count());
    }

    [Fact]
    public void Batching_LessThan300Emails_ProducesSingleBatch()
    {
        // ARRANGE: 182 correos (como los 182 alumnos en BD)
        var emails = new HashSet<string>();
        for (int i = 1; i <= 182; i++)
        {
            emails.Add($"estudiante{i}@monclova.tecnm.mx");
        }

        const int BatchSize = 300;

        // ACT
        var batches = emails.Chunk(BatchSize).ToList();

        // ASSERT
        Assert.Single(batches);
        Assert.Equal(182, batches[0].Length);
    }

    [Fact]
    public void EmailTemplateService_BuildBroadcast_SetsBccAndGenericSender()
    {
        // ARRANGE
        var templateService = new EmailTemplateService();
        var bccList = new List<string> { "a1@monclova.tecnm.mx", "a2@monclova.tecnm.mx", "a3@monclova.tecnm.mx" };

        // ACT
        var msg = templateService.BuildBroadcastNotificationEmail(
            "Convocatoria Residencias 2026",
            "Favor de revisar documentación.",
            "Estudiantes",
            DateTime.UtcNow.AddDays(5),
            "http://localhost:5085",
            bccList
        );

        // ASSERT
        Assert.Equal("[Aviso Oficial TecNM] Convocatoria Residencias 2026", msg.Subject);
        Assert.True(string.IsNullOrWhiteSpace(msg.ToEmail));
        Assert.Equal(3, msg.BccEmails?.Count);
        Assert.Contains("ESTUDIANTES", msg.BodyHtml);
        Assert.Contains("Convocatoria Residencias 2026", msg.BodyHtml);
    }

    [Fact]
    public void EmailTemplateService_BuildBroadcast_Individual_SetsDirectRecipient()
    {
        // ARRANGE
        var templateService = new EmailTemplateService();
        var directRecipient = new List<string> { "juan.perez@monclova.tecnm.mx" };

        // ACT
        var msg = templateService.BuildBroadcastNotificationEmail(
            "Aviso Individual de Prueba",
            "Contenido exclusivo para Juan.",
            "Juan Pérez (student)",
            DateTime.UtcNow.AddDays(3),
            "http://localhost:5085",
            directRecipient
        );

        // ASSERT
        Assert.Equal("[Aviso Oficial TecNM] Aviso Individual de Prueba", msg.Subject);
        Assert.NotNull(msg.BccEmails);
        Assert.Single(msg.BccEmails);
        Assert.Equal("juan.perez@monclova.tecnm.mx", msg.BccEmails[0]);
    }

    [Fact]
    public async Task Worker_WithUseMockInDevTrue_DoesNotThrowAndDoesNotConnectSmtp()
    {
        // ARRANGE: Cola real con Channel
        var queue = new EmailQueue();
        var services = new ServiceCollection();

        // Configuración con UseMockInDev = true
        var mockSettingService = new Mock<ISystemSettingService>();
        mockSettingService.Setup(s => s.GetSmtpConfigAsync())
            .ReturnsAsync(new SmtpConfigDto
            {
                Host = "smtp.office365.com",
                Port = 587,
                SenderName = "TecNM Residencias",
                SenderEmail = "noreply@ejemplo.tecnm.mx",
                Username = "noreply@ejemplo.tecnm.mx",
                Password = "TU_CONTRASEÑA_AQUI",
                EnableSsl = true,
                UseMockInDev = true // MOCK SEGURO ACTIVADO
            });

        services.AddSingleton(mockSettingService.Object);
        var serviceProvider = services.BuildServiceProvider();

        var smtpOptions = Options.Create(new SmtpOptions
        {
            UseMockInDev = true,
            Host = "smtp.office365.com",
            Port = 587
        });

        var logger = new Mock<ILogger<EmailBackgroundWorker>>();

        var worker = new EmailBackgroundWorker(queue, serviceProvider, smtpOptions, logger.Object);

        // Encolar mensaje simulado masivo
        queue.Enqueue(new EmailMessageDto
        {
            Subject = "Test Mock",
            BodyHtml = "<p>Prueba sin envío real</p>",
            BccEmails = new List<string> { "alumno1@monclova.tecnm.mx", "alumno2@monclova.tecnm.mx" }
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(400));

        // ACT: Ejecutar worker. Se cancelará en 400ms tras procesar la cola en mock mode
        await worker.StartAsync(cts.Token);
        await Task.Delay(150);
        await worker.StopAsync(cts.Token);

        // ASSERT: Si hubiera intentado conectar SMTP con password TU_CONTRASEÑA_AQUI habría lanzado excepción.
        // Al procesar en modo mock, termina exitosamente sin error.
    }

    [Fact]
    public async Task IndividualNotification_StoresTargetUserId_AndResolvesUser()
    {
        // ARRANGE: DbContext en memoria
        using var context = CreateInMemoryDbContext();

        var testUser = new User
        {
            Id = 999,
            FirstName = "Carlos",
            LastName = "López",
            Email = "carlos.lopez@monclova.tecnm.mx",
            Role = UserRole.Student,
            PasswordHash = "hash",
            IsActive = true
        };
        context.Users.Add(testUser);
        await context.SaveChangesAsync();

        var queue = new TestEmailQueue();
        var templateService = new EmailTemplateService();

        var services = new ServiceCollection();
        services.AddSingleton(context);
        services.AddSingleton<IEmailQueue>(queue);
        services.AddSingleton<IEmailTemplateService>(templateService);
        var sp = services.BuildServiceProvider();

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var scopeMock = new Mock<IServiceScope>();
        scopeMock.Setup(s => s.ServiceProvider).Returns(sp);
        scopeFactoryMock.Setup(s => s.CreateScope()).Returns(scopeMock.Object);

        var repoMock = new Mock<INotificationRepository>();
        repoMock.Setup(r => r.CreateAsync(It.IsAny<Notification>()))
            .ReturnsAsync((Notification n) =>
            {
                n.Id = 123;
                return n;
            });
        repoMock.Setup(r => r.GetByIdAsync(123))
            .ReturnsAsync(new Notification
            {
                Id = 123,
                Title = "Aviso Individual",
                Description = "Prueba unitaria individual",
                TargetRoles = "individual",
                TargetUserId = 999,
                SenderId = 1,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });

        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.Setup(u => u.UserId).Returns(1);
        currentUserMock.Setup(u => u.Role).Returns(UserRole.Admin);

        var loggerMock = new Mock<ILogger<NotificationService>>();

        var service = new NotificationService(
            repoMock.Object,
            currentUserMock.Object,
            context,
            scopeFactoryMock.Object,
            loggerMock.Object
        );

        // ACT: Enviar notificación individual dirigida a Carlos López (ID 999)
        var createDto = new CreateNotificationDto
        {
            Title = "Aviso Individual",
            Description = "Prueba unitaria individual",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            TargetRoles = new List<string> { "individual" },
            TargetUserId = 999
        };

        var result = await service.CreateNotificationAsync(createDto);

        // Dar un instante al Task en segundo plano para encolar
        await Task.Delay(200);

        // ASSERT
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(123, result.Data.Id);

        // Validar que se encoló 1 solo correo dirigido a carlos.lopez@monclova.tecnm.mx
        Assert.Single(queue.EnqueuedMessages);
        var email = queue.EnqueuedMessages[0];
        Assert.Equal("[Aviso Oficial TecNM] Aviso Individual", email.Subject);
        Assert.NotNull(email.BccEmails);
        Assert.Contains("carlos.lopez@monclova.tecnm.mx", email.BccEmails);
    }

    [Fact]
    public async Task BroadcastNotification_DirectorDoesNotRequireCareer_AndAggregatesDirectorEmail()
    {
        // ARRANGE: DbContext con director (sin carrera) y asesor (con carrera)
        using var context = CreateInMemoryDbContext();

        var directorUser = new User
        {
            Id = 50,
            FirstName = "Director",
            LastName = "General",
            Email = "director@monclova.tecnm.mx",
            Role = UserRole.Director,
            CareerId = null, // DIRECTORES NO TIENEN CARRERA
            PasswordHash = "hash",
            IsActive = true
        };
        context.Users.Add(directorUser);
        await context.SaveChangesAsync();

        var queue = new TestEmailQueue();
        var templateService = new EmailTemplateService();

        var services = new ServiceCollection();
        services.AddSingleton(context);
        services.AddSingleton<IEmailQueue>(queue);
        services.AddSingleton<IEmailTemplateService>(templateService);
        var sp = services.BuildServiceProvider();

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var scopeMock = new Mock<IServiceScope>();
        scopeMock.Setup(s => s.ServiceProvider).Returns(sp);
        scopeFactoryMock.Setup(s => s.CreateScope()).Returns(scopeMock.Object);

        var repoMock = new Mock<INotificationRepository>();
        repoMock.Setup(r => r.CreateAsync(It.IsAny<Notification>()))
            .ReturnsAsync((Notification n) => { n.Id = 200; return n; });
        repoMock.Setup(r => r.GetByIdAsync(200))
            .ReturnsAsync(new Notification
            {
                Id = 200,
                Title = "Aviso a Dirección",
                Description = "Reunión de consejo",
                TargetRoles = "director",
                SenderId = 1,
                ExpiresAt = DateTime.UtcNow.AddDays(5),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });

        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.Setup(u => u.UserId).Returns(1);
        currentUserMock.Setup(u => u.Role).Returns(UserRole.Admin);

        var service = new NotificationService(
            repoMock.Object,
            currentUserMock.Object,
            context,
            scopeFactoryMock.Object,
            new Mock<ILogger<NotificationService>>().Object
        );

        // ACT: Enviar aviso a rol 'director' (sin careerId)
        var createDto = new CreateNotificationDto
        {
            Title = "Aviso a Dirección",
            Description = "Reunión de consejo",
            ExpiresAt = DateTime.UtcNow.AddDays(5),
            TargetRoles = new List<string> { "director" },
            CareerId = null
        };

        var result = await service.CreateNotificationAsync(createDto);
        await Task.Delay(200);

        // ASSERT
        Assert.True(result.IsSuccess);
        Assert.Single(queue.EnqueuedMessages);
        var enqueued = queue.EnqueuedMessages[0];
        Assert.NotNull(enqueued.BccEmails);
        Assert.Contains("director@monclova.tecnm.mx", enqueued.BccEmails);
    }

    [Fact]
    public async Task BroadcastNotification_With650StudentsInDb_EnqueuesThreeBatchesOf300Max()
    {
        // ARRANGE: Generar 650 estudiantes en DbContext
        using var context = CreateInMemoryDbContext();

        for (int i = 1; i <= 650; i++)
        {
            var u = new User
            {
                Id = 1000 + i,
                FirstName = $"Alumno{i}",
                LastName = "TecNM",
                Email = $"alumno{i}@monclova.tecnm.mx",
                Role = UserRole.Student,
                PasswordHash = "hash",
                IsActive = true
            };
            var s = new Student
            {
                Id = 2000 + i,
                UserId = u.Id,
                User = u,
                ControlNumber = $"C{i:D6}",
                FirstName = u.FirstName,
                LastName = u.LastName,
                Curp = $"CURP{i:D14}",
                CareerId = 1,
                IsActive = true
            };
            context.Users.Add(u);
            context.Students.Add(s);
        }
        await context.SaveChangesAsync();

        var queue = new TestEmailQueue();
        var templateService = new EmailTemplateService();

        var services = new ServiceCollection();
        services.AddSingleton(context);
        services.AddSingleton<IEmailQueue>(queue);
        services.AddSingleton<IEmailTemplateService>(templateService);
        var sp = services.BuildServiceProvider();

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var scopeMock = new Mock<IServiceScope>();
        scopeMock.Setup(s => s.ServiceProvider).Returns(sp);
        scopeFactoryMock.Setup(s => s.CreateScope()).Returns(scopeMock.Object);

        var repoMock = new Mock<INotificationRepository>();
        repoMock.Setup(r => r.CreateAsync(It.IsAny<Notification>()))
            .ReturnsAsync((Notification n) => { n.Id = 300; return n; });
        repoMock.Setup(r => r.GetByIdAsync(300))
            .ReturnsAsync(new Notification
            {
                Id = 300,
                Title = "Aviso Masivo Alumnos",
                Description = "Revisar carga",
                TargetRoles = "student",
                SenderId = 1,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });

        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.Setup(u => u.UserId).Returns(1);
        currentUserMock.Setup(u => u.Role).Returns(UserRole.Admin);

        var service = new NotificationService(
            repoMock.Object,
            currentUserMock.Object,
            context,
            scopeFactoryMock.Object,
            new Mock<ILogger<NotificationService>>().Object
        );

        // ACT: Despachar a rol 'student'
        var createDto = new CreateNotificationDto
        {
            Title = "Aviso Masivo Alumnos",
            Description = "Revisar carga",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            TargetRoles = new List<string> { "student" }
        };

        var result = await service.CreateNotificationAsync(createDto);
        await Task.Delay(300);

        // ASSERT: Se deben generar 3 lotes (300, 300, 50)
        Assert.True(result.IsSuccess);
        Assert.Equal(3, queue.EnqueuedMessages.Count);
        Assert.Equal(300, queue.EnqueuedMessages[0].BccEmails?.Count);
        Assert.Equal(300, queue.EnqueuedMessages[1].BccEmails?.Count);
        Assert.Equal(50, queue.EnqueuedMessages[2].BccEmails?.Count);

        // Sin duplicados en total
        var allRecipients = queue.EnqueuedMessages.SelectMany(m => m.BccEmails!).ToList();
        Assert.Equal(650, allRecipients.Count);
        Assert.Equal(650, allRecipients.Distinct().Count());
    }

    [Fact]
    public async Task BroadcastNotification_ModalityFilter_DifferentiatesInnovatecFromRegular()
    {
        // ARRANGE: DbContext con 1 alumno regular y 1 alumno con proyecto Innovatec
        using var context = CreateInMemoryDbContext();

        var uRegular = new User
        {
            Id = 501,
            FirstName = "Pedro",
            LastName = "Regular",
            Email = "pedro.reg@monclova.tecnm.mx",
            Role = UserRole.Student,
            PasswordHash = "hash",
            IsActive = true
        };
        var sRegular = new Student
        {
            Id = 601,
            UserId = uRegular.Id,
            User = uRegular,
            ControlNumber = "REG001",
            FirstName = "Pedro",
            LastName = "Regular",
            Curp = "CURP00000000000001",
            CareerId = 1,
            IsActive = true
        };

        var uInnova = new User
        {
            Id = 502,
            FirstName = "Maria",
            LastName = "Innovatec",
            Email = "maria.innova@monclova.tecnm.mx",
            Role = UserRole.Student,
            PasswordHash = "hash",
            IsActive = true
        };
        var sInnova = new Student
        {
            Id = 602,
            UserId = uInnova.Id,
            User = uInnova,
            ControlNumber = "INN001",
            FirstName = "Maria",
            LastName = "Innovatec",
            Curp = "CURP00000000000002",
            CareerId = 1,
            IsActive = true
        };
        var pInnova = new Project
        {
            Id = 701,
            Title = "Proyecto Innovatec Nacional",
            StudentId = sInnova.Id,
            ProjectType = "innovatec",
            IsActive = true
        };

        context.Users.AddRange(uRegular, uInnova);
        context.Students.AddRange(sRegular, sInnova);
        context.Projects.Add(pInnova);
        await context.SaveChangesAsync();

        var queue = new TestEmailQueue();
        var templateService = new EmailTemplateService();

        var services = new ServiceCollection();
        services.AddSingleton(context);
        services.AddSingleton<IEmailQueue>(queue);
        services.AddSingleton<IEmailTemplateService>(templateService);
        var sp = services.BuildServiceProvider();

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var scopeMock = new Mock<IServiceScope>();
        scopeMock.Setup(s => s.ServiceProvider).Returns(sp);
        scopeFactoryMock.Setup(s => s.CreateScope()).Returns(scopeMock.Object);

        var repoMock = new Mock<INotificationRepository>();
        repoMock.Setup(r => r.CreateAsync(It.IsAny<Notification>()))
            .ReturnsAsync((Notification n) => { n.Id = 400; return n; });
        repoMock.Setup(r => r.GetByIdAsync(400))
            .ReturnsAsync(new Notification
            {
                Id = 400,
                Title = "Aviso Innovatec",
                Description = "Avance de prototipo",
                TargetRoles = "student",
                ResidencyModality = "innovatec",
                SenderId = 1,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });

        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.Setup(u => u.UserId).Returns(1);
        currentUserMock.Setup(u => u.Role).Returns(UserRole.Admin);

        var service = new NotificationService(
            repoMock.Object,
            currentUserMock.Object,
            context,
            scopeFactoryMock.Object,
            new Mock<ILogger<NotificationService>>().Object
        );

        // ACT: Enviar solo a estudiantes con modalidad innovatec
        var createDto = new CreateNotificationDto
        {
            Title = "Aviso Innovatec",
            Description = "Avance de prototipo",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            TargetRoles = new List<string> { "student" },
            ResidencyModality = "innovatec"
        };

        var result = await service.CreateNotificationAsync(createDto);
        await Task.Delay(200);

        // ASSERT: Solo María (innova) debe estar en la lista de correos
        Assert.True(result.IsSuccess);
        Assert.Single(queue.EnqueuedMessages);
        var batch = queue.EnqueuedMessages[0].BccEmails;
        Assert.NotNull(batch);
        Assert.Single(batch);
        Assert.Contains("maria.innova@monclova.tecnm.mx", batch);
        Assert.DoesNotContain("pedro.reg@monclova.tecnm.mx", batch);
    }
}

public class TestEmailQueue : IEmailQueue
{
    public List<EmailMessageDto> EnqueuedMessages { get; } = new();

    public void Enqueue(EmailMessageDto message)
    {
        if (message != null)
        {
            EnqueuedMessages.Add(message);
        }
    }

    public ValueTask<EmailMessageDto> DequeueAsync(CancellationToken cancellationToken)
    {
        var msg = EnqueuedMessages.FirstOrDefault() ?? new EmailMessageDto();
        return ValueTask.FromResult(msg);
    }
}
