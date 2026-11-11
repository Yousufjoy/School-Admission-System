using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Options;

namespace SchoolAdmission.Tests;

/// <summary>
/// Shared <see cref="WebApplicationFactory{TEntryPoint}"/> configured with an isolated in-memory database per test instance.
/// </summary>
public sealed class AdmissionTestFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"admission_test_{Guid.NewGuid():N}";
    private readonly int? _holdDurationSeconds;

    public AdmissionTestFactory(int? holdDurationSeconds = null)
    {
        _holdDurationSeconds = holdDurationSeconds;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        if (_holdDurationSeconds.HasValue)
        {
            builder.UseSetting("HOLD_DURATION_SECONDS", _holdDurationSeconds.Value.ToString());
        }

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<AppDbContext>();

            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));

            if (_holdDurationSeconds.HasValue)
            {
                services.RemoveAll<HoldOptions>();
                services.AddSingleton(new HoldOptions
                {
                    HoldDurationSeconds = _holdDurationSeconds.Value
                });
            }
        });
    }

    public static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(body);
    }

    public static HttpRequestMessage CreateRequest(HttpMethod method, string url, string? studentId = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (studentId is not null)
        {
            request.Headers.Add("X-Student-Id", studentId);
        }

        return request;
    }
}

