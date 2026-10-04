using FindIt.Api.Data;
using FindIt.Api.Domain;
using FindIt.Api.Services;
using System.Security.Claims;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace FindIt.Api.Tests;

public sealed class RecoveryWorkflowTests
{
    private static readonly DateTime FridayApproval =
        new(2026, 9, 25, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ApprovalExpiresAtEndOfFifthBusinessDayInBrasilia()
    {
        var deadline = BusinessDayDeadline.FiveBusinessDaysAfter(FridayApproval);

        // Friday 25 September -> Friday 2 October, 23:59 in Brasília.
        Assert.Equal(new DateTime(2026, 10, 3, 3, 0, 0, DateTimeKind.Utc), deadline);
    }

    [Fact]
    public async Task ApprovalRejectsOtherPendingRequestsAndKeepsItemActive()
    {
        await using var fixture = await Fixture.CreateAsync();
        var first = fixture.AddRequest(userId: 1);
        var second = fixture.AddRequest(userId: 2);
        await fixture.Db.SaveChangesAsync();

        var workflow = new RecoveryWorkflowService(fixture.Db, new FixedClock(FridayApproval));
        await workflow.ApproveAsync(first.Id, administratorId: 3, "Documento conferido", default);

        Assert.Equal(RequestStatus.Approved, first.Status);
        Assert.Equal(RequestStatus.Rejected, second.Status);
        Assert.Equal("Outra solicitação para este item foi aprovada.", second.AdminNotes);
        Assert.Equal(ItemStatus.Active, fixture.Item.Status);
        Assert.Equal(BusinessDayDeadline.FiveBusinessDaysAfter(FridayApproval), first.ExpiresAt);
        Assert.Equal(2, await fixture.Db.ActivityLogs.CountAsync());
    }

    [Fact]
    public async Task ExpirationReopensItemAndRecordsSystemAction()
    {
        await using var fixture = await Fixture.CreateAsync();
        var request = fixture.AddRequest(userId: 1, status: RequestStatus.Approved);
        request.ExpiresAt = FridayApproval.AddMinutes(-1);
        await fixture.Db.SaveChangesAsync();

        var count = await new RequestExpirationService(fixture.Db)
            .ExpireDueAsync(FridayApproval, default);

        Assert.Equal(1, count);
        Assert.Equal(RequestStatus.Expired, request.Status);
        var activity = await fixture.Db.ActivityLogs.SingleAsync();
        Assert.Equal(ActivityType.RequestExpired, activity.Type);
        Assert.Null(activity.UserId);
    }

    [Fact]
    public async Task ReturnedItemDoesNotExpireItsApprovedRequest()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Item.Status = ItemStatus.Returned;
        var request = fixture.AddRequest(userId: 1, status: RequestStatus.Approved);
        request.ExpiresAt = FridayApproval.AddMinutes(-1);
        await fixture.Db.SaveChangesAsync();

        var count = await new RequestExpirationService(fixture.Db)
            .ExpireDueAsync(FridayApproval, default);

        Assert.Equal(0, count);
        Assert.Equal(RequestStatus.Approved, request.Status);
    }

    [Fact]
    public async Task PhysicalReturnChangesItemOnlyAfterApprovedRequest()
    {
        await using var fixture = await Fixture.CreateAsync();
        var request = fixture.AddRequest(userId: 1, status: RequestStatus.Approved);
        request.ExpiresAt = FridayApproval.AddDays(1);
        await fixture.Db.SaveChangesAsync();

        var workflow = new RecoveryWorkflowService(fixture.Db, new FixedClock(FridayApproval));
        await workflow.ReturnAsync(fixture.Item.Id, administratorId: 3, default);

        Assert.Equal(ItemStatus.Returned, fixture.Item.Status);
        Assert.Equal(RequestStatus.Approved, request.Status);
        Assert.Equal(ActivityType.ItemReturned,
            (await fixture.Db.ActivityLogs.SingleAsync()).Type);
        Assert.Equal(0, await new RequestExpirationService(fixture.Db)
            .ExpireDueAsync(FridayApproval.AddDays(2), default));
    }

    [Fact]
    public async Task ExceptionalReturnClosesOpenRequests()
    {
        await using var fixture = await Fixture.CreateAsync();
        var approved = fixture.AddRequest(userId: 1, status: RequestStatus.Approved);
        var pending = fixture.AddRequest(userId: 2);
        await fixture.Db.SaveChangesAsync();

        var workflow = new RecoveryWorkflowService(fixture.Db, new FixedClock(FridayApproval));
        await workflow.OverrideStatusAsync(fixture.Item.Id, 3, ItemStatus.Returned,
            "Entrega presencial ao visitante", default);

        Assert.Equal(ItemStatus.Returned, fixture.Item.Status);
        Assert.Equal(RequestStatus.Cancelled, approved.Status);
        Assert.Equal(RequestStatus.Rejected, pending.Status);
        Assert.Equal("Entrega presencial ao visitante", approved.AdminNotes);
        Assert.Equal(3, await fixture.Db.ActivityLogs.CountAsync());
    }

    [Fact]
    public async Task QrCodeCanOnlyBeLinkedOnce()
    {
        await using var fixture = await Fixture.CreateAsync();
        var items = new ItemManagementService(fixture.Db, new FixedClock(FridayApproval));
        var qrCode = Assert.Single(await items.GenerateQRCodesAsync(3, 1, default));
        var input = new NewItem(fixture.Item.CategoryId, qrCode.Guid, "Livro",
            "Livro azul", "Biblioteca", FridayApproval);

        var itemId = await items.CreateAsync(3, input, default);
        Assert.Equal(qrCode.Id, (await fixture.Db.Items.FindAsync(itemId))!.QRCodeId);
        await Assert.ThrowsAsync<BusinessConflictException>(() => items.CreateAsync(3, input, default));
    }

    [Fact]
    public async Task ReactivationDoesNotReopenOldApprovedRequest()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Item.Status = ItemStatus.Returned;
        var request = fixture.AddRequest(userId: 1, status: RequestStatus.Approved);
        await fixture.Db.SaveChangesAsync();

        var workflow = new RecoveryWorkflowService(fixture.Db, new FixedClock(FridayApproval));
        await workflow.OverrideStatusAsync(fixture.Item.Id, 3, ItemStatus.Active,
            "Devolução registrada por engano", default);

        Assert.Equal(ItemStatus.Active, fixture.Item.Status);
        Assert.Equal(RequestStatus.Cancelled, request.Status);
    }

    [Fact]
    public async Task UserRegistrationRequiresInstitutionalEmailAndStableIdentifiers()
    {
        await using var fixture = await Fixture.CreateAsync();
        var service = new CurrentUserService(fixture.Db, new FixedClock(FridayApproval),
            new ConfigurationBuilder().Build(), new TestHostEnvironment());
        var tenantId = Guid.Parse("e298359b-9b54-45c0-9bb7-690913b34cf8");
        var objectId = Guid.NewGuid();
        var account = Principal(tenantId, objectId, "aluno@discente.uemg.br");

        var user = await service.GetOrCreateAsync(account, default);
        var sameUser = await service.GetOrCreateAsync(account, default);

        Assert.Equal(user.Id, sameUser.Id);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetOrCreateAsync(
            Principal(tenantId, Guid.NewGuid(), "pessoa@outlook.com"), default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetOrCreateAsync(
            Principal(Guid.NewGuid(), Guid.NewGuid(), "aluno@discente.uemg.br"), default));
    }

    [Fact]
    public async Task AnyMicrosoftAccountRequiresDevelopmentTestMode()
    {
        await using var fixture = await Fixture.CreateAsync();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["FindItAuth:AllowAnyMicrosoftAccountForTesting"] = "true",
                ["AzureAd:TenantId"] = "common"
            }).Build();
        var consumerTenant = Guid.Parse("9188040d-6c67-4c5b-b112-36a304b66dad");
        var personal = Principal(consumerTenant, Guid.NewGuid(), "pessoa@outlook.com");

        var developmentService = new CurrentUserService(fixture.Db,
            new FixedClock(FridayApproval), configuration,
            new TestHostEnvironment { EnvironmentName = Environments.Development });
        var user = await developmentService.GetOrCreateAsync(personal, default);
        Assert.Equal("pessoa@outlook.com", user.Email);

        var productionService = new CurrentUserService(fixture.Db,
            new FixedClock(FridayApproval), configuration, new TestHostEnvironment());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            productionService.GetOrCreateAsync(personal, default));
        var otherOrganization = await developmentService.GetOrCreateAsync(
            Principal(Guid.NewGuid(), Guid.NewGuid(), "pessoa@outro-tenant.com"), default);
        Assert.Equal("pessoa@outro-tenant.com", otherOrganization.Email);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "FindIt.Api.Tests";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    [Fact]
    public async Task NewRequestCanOpenImmediatelyAfterPreviousApprovalExpires()
    {
        await using var fixture = await Fixture.CreateAsync();
        var previous = fixture.AddRequest(userId: 1, status: RequestStatus.Approved);
        previous.ExpiresAt = FridayApproval.AddSeconds(-1);
        await fixture.Db.SaveChangesAsync();

        var workflow = new RecoveryWorkflowService(fixture.Db, new FixedClock(FridayApproval));
        var newRequestId = await workflow.CreateAsync(fixture.Item.Id, 2,
            "Reconheço este item.", default);

        Assert.True(newRequestId > previous.Id);
        Assert.Equal(RequestStatus.Expired, previous.Status);
        Assert.Equal(1, await fixture.Db.RecoveryRequests.CountAsync(x =>
            x.ItemId == fixture.Item.Id && x.Status == RequestStatus.Pending));
    }

    private static ClaimsPrincipal Principal(Guid tenantId, Guid objectId, string email) =>
        new(new ClaimsIdentity(
        [
            new Claim("tid", tenantId.ToString()),
            new Claim("oid", objectId.ToString()),
            new Claim("preferred_username", email)
        ], "test"));

    private sealed class FixedClock(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }

    private sealed class Fixture(SqliteConnection connection, FindItDbContext db, Item item)
        : IAsyncDisposable
    {
        public FindItDbContext Db { get; } = db;
        public Item Item { get; } = item;

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<FindItDbContext>()
                .UseSqlite(connection).Options;
            var db = new FindItDbContext(options);
            await db.Database.EnsureCreatedAsync();

            var category = new Category { Name = "Outros", CreatedAt = FridayApproval };
            var item = new Item
            {
                Category = category,
                Name = "Chave",
                Description = "Chave encontrada",
                Location = "Biblioteca",
                FoundOn = FridayApproval,
                CreatedAt = FridayApproval,
                UpdatedAt = FridayApproval,
                StatusChangedAt = FridayApproval
            };
            db.Items.Add(item);
            for (var i = 1; i <= 3; i++)
            {
                db.Users.Add(new User
                {
                    TenantId = Guid.NewGuid(),
                    ObjectId = Guid.NewGuid(),
                    Name = $"Usuário {i}",
                    Email = $"user{i}@discente.uemg.br",
                    IsAdmin = i == 3,
                    CreatedAt = FridayApproval
                });
            }
            await db.SaveChangesAsync();
            return new Fixture(connection, db, item);
        }

        public RecoveryRequest AddRequest(int userId, RequestStatus status = RequestStatus.Pending)
        {
            var request = new RecoveryRequest
            {
                ItemId = Item.Id,
                UserId = userId,
                Justification = "É meu item.",
                Status = status,
                CreatedAt = FridayApproval,
                UpdatedAt = FridayApproval
            };
            Db.RecoveryRequests.Add(request);
            return request;
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
