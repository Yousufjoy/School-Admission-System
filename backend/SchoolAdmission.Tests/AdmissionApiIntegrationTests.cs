using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Middleware;
using SchoolAdmission.Api.Models;
using SchoolAdmission.Api.Options;

namespace SchoolAdmission.Tests;

public sealed class AdmissionApiIntegrationTests
{
    private const string StudentA = "12345678901234567";
    private const string StudentB = "76543210987654321";

    [Fact]
    public void HoldOptions_ResolvesDefaultAndOverrides()
    {
        var original = Environment.GetEnvironmentVariable("HOLD_DURATION_SECONDS");
        try
        {
            Environment.SetEnvironmentVariable("HOLD_DURATION_SECONDS", null);
            var emptyConfig = new ConfigurationBuilder().Build();
            Assert.Equal(600, HoldOptions.ResolveHoldDurationSeconds(emptyConfig));

            var customConfig = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["HOLD_DURATION_SECONDS"] = "120"
                })
                .Build();
            Assert.Equal(120, HoldOptions.ResolveHoldDurationSeconds(customConfig));

            var invalidConfig = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["HOLD_DURATION_SECONDS"] = "-5"
                })
                .Build();
            Assert.Equal(600, HoldOptions.ResolveHoldDurationSeconds(invalidConfig));
        }
        finally
        {
            Environment.SetEnvironmentVariable("HOLD_DURATION_SECONDS", original);
        }
    }

    [Fact]
    public async Task ClassesEndpoints_CreateListAndGetById_ReturnExactCamelCaseAnd404()
    {
        using var factory = new AdmissionTestFactory();
        using var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/classes", new { name = "Grade 1A", totalSeats = 3 });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var createdJson = await AdmissionTestFactory.ReadJsonAsync(createResponse);
        var root = createdJson.RootElement;
        var classId = root.GetProperty("id").GetString();
        Assert.NotNull(classId);
        Assert.StartsWith("c_", classId);
        Assert.Equal("Grade 1A", root.GetProperty("name").GetString());
        Assert.Equal(3, root.GetProperty("totalSeats").GetInt32());

        var listResponse = await client.GetAsync("/classes");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        using var listJson = await AdmissionTestFactory.ReadJsonAsync(listResponse);
        Assert.Equal(JsonValueKind.Array, listJson.RootElement.ValueKind);
        var listed = listJson.RootElement[0];
        Assert.Equal(classId, listed.GetProperty("id").GetString());
        Assert.Equal("Grade 1A", listed.GetProperty("name").GetString());
        Assert.Equal(3, listed.GetProperty("totalSeats").GetInt32());
        Assert.Equal(0, listed.GetProperty("heldSeats").GetInt32());
        Assert.Equal(0, listed.GetProperty("confirmedSeats").GetInt32());
        Assert.Equal(3, listed.GetProperty("availableSeats").GetInt32());

        var singleResponse = await client.GetAsync($"/classes/{classId}");
        Assert.Equal(HttpStatusCode.OK, singleResponse.StatusCode);
        using var singleJson = await AdmissionTestFactory.ReadJsonAsync(singleResponse);
        Assert.Equal(classId, singleJson.RootElement.GetProperty("id").GetString());

        var missingResponse = await client.GetAsync("/classes/c_missing1");
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
    }

    [Fact]
    public async Task CreateHold_ValidatesStudentHeaderEnforcesDuplicatesCapacityAndDuration()
    {
        using var factory = new AdmissionTestFactory(holdDurationSeconds: 180);
        using var client = factory.CreateClient();

        var classResponse = await client.PostAsJsonAsync("/classes", new { name = "Biology", totalSeats = 1 });
        using var classDoc = await AdmissionTestFactory.ReadJsonAsync(classResponse);
        var classId = classDoc.RootElement.GetProperty("id").GetString()!;

        // Missing header -> 400
        var missingHeader = await client.SendAsync(AdmissionTestFactory.CreateRequest(HttpMethod.Post, $"/classes/{classId}/holds", null));
        Assert.Equal(HttpStatusCode.BadRequest, missingHeader.StatusCode);

        // Invalid format -> 400
        var shortHeader = await client.SendAsync(AdmissionTestFactory.CreateRequest(HttpMethod.Post, $"/classes/{classId}/holds", "12345"));
        Assert.Equal(HttpStatusCode.BadRequest, shortHeader.StatusCode);

        var alphaHeader = await client.SendAsync(AdmissionTestFactory.CreateRequest(HttpMethod.Post, $"/classes/{classId}/holds", "1234567890123456a"));
        Assert.Equal(HttpStatusCode.BadRequest, alphaHeader.StatusCode);

        // Unknown class -> 404
        var missingClass = await client.SendAsync(AdmissionTestFactory.CreateRequest(HttpMethod.Post, "/classes/c_unknown1/holds", StudentA));
        Assert.Equal(HttpStatusCode.NotFound, missingClass.StatusCode);

        // Valid hold -> 201 with exact camelCase and ISO-8601 UTC
        var before = DateTime.UtcNow;
        var holdResponse = await client.SendAsync(AdmissionTestFactory.CreateRequest(HttpMethod.Post, $"/classes/{classId}/holds", StudentA));
        Assert.Equal(HttpStatusCode.Created, holdResponse.StatusCode);

        using var holdDoc = await AdmissionTestFactory.ReadJsonAsync(holdResponse);
        var holdRoot = holdDoc.RootElement;
        var holdId = holdRoot.GetProperty("holdId").GetString()!;
        Assert.StartsWith("h_", holdId);
        Assert.Equal(classId, holdRoot.GetProperty("classId").GetString());
        Assert.Equal(StudentA, holdRoot.GetProperty("studentId").GetString());
        Assert.Equal("HELD", holdRoot.GetProperty("status").GetString());

        var expiresRaw = holdRoot.GetProperty("expiresAt").GetString()!;
        Assert.EndsWith("Z", expiresRaw);
        var expiresAt = DateTime.Parse(expiresRaw, null, System.Globalization.DateTimeStyles.RoundtripKind);
        Assert.InRange(expiresAt, before.AddSeconds(175), DateTime.UtcNow.AddSeconds(185));

        // Duplicate active hold for same student -> 409
        var duplicateHold = await client.SendAsync(AdmissionTestFactory.CreateRequest(HttpMethod.Post, $"/classes/{classId}/holds", StudentA));
        Assert.Equal(HttpStatusCode.Conflict, duplicateHold.StatusCode);

        // Class full for another student -> 409
        var fullClassHold = await client.SendAsync(AdmissionTestFactory.CreateRequest(HttpMethod.Post, $"/classes/{classId}/holds", StudentB));
        Assert.Equal(HttpStatusCode.Conflict, fullClassHold.StatusCode);

        // Seat counts updated
        var classAfterHold = await client.GetAsync($"/classes/{classId}");
        using var afterDoc = await AdmissionTestFactory.ReadJsonAsync(classAfterHold);
        Assert.Equal(1, afterDoc.RootElement.GetProperty("heldSeats").GetInt32());
        Assert.Equal(0, afterDoc.RootElement.GetProperty("confirmedSeats").GetInt32());
        Assert.Equal(0, afterDoc.RootElement.GetProperty("availableSeats").GetInt32());
    }

    [Fact]
    public async Task ConfirmCancelAndMeHolds_EnforceOwnershipIdempotencyAndSeatUpdates()
    {
        using var factory = new AdmissionTestFactory();
        using var client = factory.CreateClient();

        var classResponse = await client.PostAsJsonAsync("/classes", new { name = "Physics", totalSeats = 1 });
        using var classDoc = await AdmissionTestFactory.ReadJsonAsync(classResponse);
        var classId = classDoc.RootElement.GetProperty("id").GetString()!;

        // Hold for Student A
        var holdResponse = await client.SendAsync(AdmissionTestFactory.CreateRequest(HttpMethod.Post, $"/classes/{classId}/holds", StudentA));
        using var holdDoc = await AdmissionTestFactory.ReadJsonAsync(holdResponse);
        var holdId = holdDoc.RootElement.GetProperty("holdId").GetString()!;

        // Wrong student cannot confirm -> 403
        var wrongConfirm = await client.SendAsync(AdmissionTestFactory.CreateRequest(HttpMethod.Post, $"/holds/{holdId}/confirm", StudentB));
        Assert.Equal(HttpStatusCode.Forbidden, wrongConfirm.StatusCode);

        // Wrong student cannot cancel -> 403
        var wrongDelete = await client.SendAsync(AdmissionTestFactory.CreateRequest(HttpMethod.Delete, $"/holds/{holdId}", StudentB));
        Assert.Equal(HttpStatusCode.Forbidden, wrongDelete.StatusCode);

        // Confirm by owner -> 200 CONFIRMED
        var confirm1 = await client.SendAsync(AdmissionTestFactory.CreateRequest(HttpMethod.Post, $"/holds/{holdId}/confirm", StudentA));
        Assert.Equal(HttpStatusCode.OK, confirm1.StatusCode);
        using var confirmDoc1 = await AdmissionTestFactory.ReadJsonAsync(confirm1);
        Assert.Equal(holdId, confirmDoc1.RootElement.GetProperty("holdId").GetString());
        Assert.Equal("CONFIRMED", confirmDoc1.RootElement.GetProperty("status").GetString());

        // Idempotent second confirm -> 200 CONFIRMED
        var confirm2 = await client.SendAsync(AdmissionTestFactory.CreateRequest(HttpMethod.Post, $"/holds/{holdId}/confirm", StudentA));
        Assert.Equal(HttpStatusCode.OK, confirm2.StatusCode);
        using var confirmDoc2 = await AdmissionTestFactory.ReadJsonAsync(confirm2);
        Assert.Equal("CONFIRMED", confirmDoc2.RootElement.GetProperty("status").GetString());

        // Duplicate hold after confirm -> 409
        var duplicateAfterConfirm = await client.SendAsync(AdmissionTestFactory.CreateRequest(HttpMethod.Post, $"/classes/{classId}/holds", StudentA));
        Assert.Equal(HttpStatusCode.Conflict, duplicateAfterConfirm.StatusCode);

        // GET /me/holds for Student A
        var meResponse = await client.SendAsync(AdmissionTestFactory.CreateRequest(HttpMethod.Get, "/me/holds", StudentA));
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        using var meDoc = await AdmissionTestFactory.ReadJsonAsync(meResponse);
        Assert.Equal(1, meDoc.RootElement.GetArrayLength());
        var myHold = meDoc.RootElement[0];
        Assert.Equal(holdId, myHold.GetProperty("holdId").GetString());
        Assert.Equal(classId, myHold.GetProperty("classId").GetString());
        Assert.Equal("Physics", myHold.GetProperty("className").GetString());
        Assert.Equal("CONFIRMED", myHold.GetProperty("status").GetString());
        Assert.EndsWith("Z", myHold.GetProperty("expiresAt").GetString()!);

        // Delete by owner -> 204 No Content and frees seat
        var deleteResponse = await client.SendAsync(AdmissionTestFactory.CreateRequest(HttpMethod.Delete, $"/holds/{holdId}", StudentA));
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Confirming cancelled hold -> 409 Conflict
        var confirmCancelled = await client.SendAsync(AdmissionTestFactory.CreateRequest(HttpMethod.Post, $"/holds/{holdId}/confirm", StudentA));
        Assert.Equal(HttpStatusCode.Conflict, confirmCancelled.StatusCode);

        // Seat is free again for Student B
        var holdBResponse = await client.SendAsync(AdmissionTestFactory.CreateRequest(HttpMethod.Post, $"/classes/{classId}/holds", StudentB));
        Assert.Equal(HttpStatusCode.Created, holdBResponse.StatusCode);
    }

    [Fact]
    public async Task ExpiredHolds_ReportExpiredStatusAndFreeSeats()
    {
        using var factory = new AdmissionTestFactory();
        using var client = factory.CreateClient();

        var classResponse = await client.PostAsJsonAsync("/classes", new { name = "Chemistry", totalSeats = 1 });
        using var classDoc = await AdmissionTestFactory.ReadJsonAsync(classResponse);
        var classId = classDoc.RootElement.GetProperty("id").GetString()!;

        var expiredHoldId = "h_deadbeef";
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Holds.Add(new HoldEntity
            {
                Id = expiredHoldId,
                ClassId = classId,
                StudentId = StudentA,
                Status = HoldStatuses.Held,
                CreatedAt = DateTime.UtcNow.AddMinutes(-20),
                ExpiresAt = DateTime.UtcNow.AddMinutes(-5)
            });
            await db.SaveChangesAsync();
        }

        // GET /holds/{holdId} returns EXPIRED
        var holdGet = await client.GetAsync($"/holds/{expiredHoldId}");
        Assert.Equal(HttpStatusCode.OK, holdGet.StatusCode);
        using var holdGetDoc = await AdmissionTestFactory.ReadJsonAsync(holdGet);
        Assert.Equal(expiredHoldId, holdGetDoc.RootElement.GetProperty("holdId").GetString());
        Assert.Equal(classId, holdGetDoc.RootElement.GetProperty("classId").GetString());
        Assert.Equal("EXPIRED", holdGetDoc.RootElement.GetProperty("status").GetString());
        Assert.EndsWith("Z", holdGetDoc.RootElement.GetProperty("expiresAt").GetString()!);

        // Confirming expired hold -> 409 Conflict
        var confirmExpired = await client.SendAsync(AdmissionTestFactory.CreateRequest(HttpMethod.Post, $"/holds/{expiredHoldId}/confirm", StudentA));
        Assert.Equal(HttpStatusCode.Conflict, confirmExpired.StatusCode);

        // Class shows 0 heldSeats and 1 availableSeat
        var classGet = await client.GetAsync($"/classes/{classId}");
        using var classGetDoc = await AdmissionTestFactory.ReadJsonAsync(classGet);
        Assert.Equal(0, classGetDoc.RootElement.GetProperty("heldSeats").GetInt32());
        Assert.Equal(1, classGetDoc.RootElement.GetProperty("availableSeats").GetInt32());

        // Student A can place a new hold after expiration
        var retryHold = await client.SendAsync(AdmissionTestFactory.CreateRequest(HttpMethod.Post, $"/classes/{classId}/holds", StudentA));
        Assert.Equal(HttpStatusCode.Created, retryHold.StatusCode);
    }

    [Fact]
    public async Task InvalidInputsMalformedRoutesAndCors_AreHandledCleanlyWithout500()
    {
        using var factory = new AdmissionTestFactory();
        using var client = factory.CreateClient();

        // Invalid class creation payloads -> 400
        var badSeats = await client.PostAsJsonAsync("/classes", new { name = "Art", totalSeats = 0 });
        Assert.Equal(HttpStatusCode.BadRequest, badSeats.StatusCode);

        var emptyName = await client.PostAsJsonAsync("/classes", new { name = "   ", totalSeats = 10 });
        Assert.Equal(HttpStatusCode.BadRequest, emptyName.StatusCode);

        var malformedJson = new StringContent("{bad json", Encoding.UTF8, "application/json");
        var badJsonResponse = await client.PostAsync("/classes", malformedJson);
        Assert.Equal(HttpStatusCode.BadRequest, badJsonResponse.StatusCode);

        // Invalid route parameter -> 400
        var invalidRouteParam = await client.GetAsync("/classes/%20");
        Assert.Equal(HttpStatusCode.BadRequest, invalidRouteParam.StatusCode);

        var oversizedRouteParam = await client.GetAsync($"/holds/{new string('a', 40)}");
        Assert.Equal(HttpStatusCode.BadRequest, oversizedRouteParam.StatusCode);

        // CORS preflight from React dev port
        var preflight = new HttpRequestMessage(HttpMethod.Options, "/classes");
        preflight.Headers.Add("Origin", "http://localhost:5173");
        preflight.Headers.Add("Access-Control-Request-Method", "POST");
        preflight.Headers.Add("Access-Control-Request-Headers", "content-type,x-student-id");
        var corsResponse = await client.SendAsync(preflight);
        Assert.True(
            corsResponse.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.OK,
            $"Unexpected CORS preflight status: {corsResponse.StatusCode}");
        Assert.Contains("*", corsResponse.Headers.GetValues("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task GlobalExceptionMiddleware_MapsUnhandledExceptionsToNon500Json()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var middleware = new GlobalExceptionMiddleware(
            _ => throw new InvalidOperationException("Unexpected failure"),
            NullLogger<GlobalExceptionMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var doc = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal(400, doc.RootElement.GetProperty("statusCode").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(doc.RootElement.GetProperty("error").GetString()));
    }
}
