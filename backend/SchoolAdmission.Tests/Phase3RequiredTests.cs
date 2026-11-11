using System.Net;
using System.Net.Http.Json;

namespace SchoolAdmission.Tests;

public sealed class Phase3RequiredTests
{
    private const string ValidStudentId = "20261234567890123";

    [Theory]
    [InlineData("1234567890")]
    [InlineData("abcdefghijklmnopq")]
    [InlineData("invalidstudentid")]
    public async Task Test1_Validation_Returns400_WhenStudentIdIsInvalid(string invalidStudentId)
    {
        using var factory = new AdmissionTestFactory();
        using var client = factory.CreateClient();

        using var classResponse = await client.PostAsJsonAsync("/classes", new
        {
            name = "Math 101",
            totalSeats = 10
        });
        Assert.Equal(HttpStatusCode.Created, classResponse.StatusCode);

        using var classJson = await AdmissionTestFactory.ReadJsonAsync(classResponse);
        var classId = classJson.RootElement.GetProperty("id").GetString()!;

        using var holdRequest = AdmissionTestFactory.CreateRequest(
            HttpMethod.Post,
            $"/classes/{classId}/holds",
            invalidStudentId);

        using var holdResponse = await client.SendAsync(holdRequest);
        Assert.Equal(HttpStatusCode.BadRequest, holdResponse.StatusCode);
    }

    [Fact]
    public async Task Test2_IdempotentConfirm_DoesNotDuplicateSeats()
    {
        using var factory = new AdmissionTestFactory();
        using var client = factory.CreateClient();

        using var classResponse = await client.PostAsJsonAsync("/classes", new
        {
            name = "History 201",
            totalSeats = 5
        });
        Assert.Equal(HttpStatusCode.Created, classResponse.StatusCode);

        using var classJson = await AdmissionTestFactory.ReadJsonAsync(classResponse);
        var classId = classJson.RootElement.GetProperty("id").GetString()!;

        using var holdRequest = AdmissionTestFactory.CreateRequest(
            HttpMethod.Post,
            $"/classes/{classId}/holds",
            ValidStudentId);
        using var holdResponse = await client.SendAsync(holdRequest);
        Assert.Equal(HttpStatusCode.Created, holdResponse.StatusCode);

        using var holdJson = await AdmissionTestFactory.ReadJsonAsync(holdResponse);
        var holdId = holdJson.RootElement.GetProperty("holdId").GetString()!;

        using var confirmReq1 = AdmissionTestFactory.CreateRequest(
            HttpMethod.Post,
            $"/holds/{holdId}/confirm",
            ValidStudentId);
        using var confirmResp1 = await client.SendAsync(confirmReq1);
        Assert.Equal(HttpStatusCode.OK, confirmResp1.StatusCode);
        using var confirmJson1 = await AdmissionTestFactory.ReadJsonAsync(confirmResp1);
        Assert.Equal("CONFIRMED", confirmJson1.RootElement.GetProperty("status").GetString());

        using var confirmReq2 = AdmissionTestFactory.CreateRequest(
            HttpMethod.Post,
            $"/holds/{holdId}/confirm",
            ValidStudentId);
        using var confirmResp2 = await client.SendAsync(confirmReq2);
        Assert.Equal(HttpStatusCode.OK, confirmResp2.StatusCode);
        using var confirmJson2 = await AdmissionTestFactory.ReadJsonAsync(confirmResp2);
        Assert.Equal("CONFIRMED", confirmJson2.RootElement.GetProperty("status").GetString());

        using var classGetResponse = await client.GetAsync($"/classes/{classId}");
        Assert.Equal(HttpStatusCode.OK, classGetResponse.StatusCode);
        using var classSummaryJson = await AdmissionTestFactory.ReadJsonAsync(classGetResponse);
        Assert.Equal(1, classSummaryJson.RootElement.GetProperty("confirmedSeats").GetInt32());
        Assert.Equal(0, classSummaryJson.RootElement.GetProperty("heldSeats").GetInt32());
        Assert.Equal(4, classSummaryJson.RootElement.GetProperty("availableSeats").GetInt32());
    }

    [Fact]
    public async Task Test3_HighConcurrency_OversellingPrevention()
    {
        using var factory = new AdmissionTestFactory();
        using var client = factory.CreateClient();

        using var classResponse = await client.PostAsJsonAsync("/classes", new
        {
            name = "Computer Science",
            totalSeats = 5
        });
        Assert.Equal(HttpStatusCode.Created, classResponse.StatusCode);

        using var classJson = await AdmissionTestFactory.ReadJsonAsync(classResponse);
        var classId = classJson.RootElement.GetProperty("id").GetString()!;

        var startGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var tasks = Enumerable.Range(1, 50).Select(async index =>
        {
            var studentId = $"2026000000000{index:D4}";
            await startGate.Task;

            using var request = AdmissionTestFactory.CreateRequest(
                HttpMethod.Post,
                $"/classes/{classId}/holds",
                studentId);

            return await client.SendAsync(request);
        }).ToArray();

        startGate.SetResult();
        var responses = await Task.WhenAll(tasks);

        try
        {
            Assert.Equal(5, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
            Assert.Equal(45, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }

        using var classGetResponse = await client.GetAsync($"/classes/{classId}");
        Assert.Equal(HttpStatusCode.OK, classGetResponse.StatusCode);

        using var classSummaryJson = await AdmissionTestFactory.ReadJsonAsync(classGetResponse);
        Assert.Equal(5, classSummaryJson.RootElement.GetProperty("heldSeats").GetInt32());
        Assert.Equal(0, classSummaryJson.RootElement.GetProperty("availableSeats").GetInt32());
    }
}

