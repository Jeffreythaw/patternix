using Microsoft.EntityFrameworkCore;
using Patternix.Api.Domain;
using Patternix.Api.Data;
using Patternix.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

builder.Services.AddDbContext<PatternixDbContext>(options =>
{
    var connectionString =
        Environment.GetEnvironmentVariable("LS_CONNECTION_STRING")
        ?? builder.Configuration.GetConnectionString("LS");

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException("Connection string is not configured.");
    }

    options.UseSqlServer(connectionString);
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddScoped<InputParser>();
builder.Services.AddScoped<TheoryEngine>();
builder.Services.AddScoped<CandidateEngine>();
builder.Services.AddScoped<DatasetService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("AllowAll");
app.MapControllers();

await EnsureDatabaseAsync(app);

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

static async Task EnsureDatabaseAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<PatternixDbContext>();
    var parser = scope.ServiceProvider.GetRequiredService<InputParser>();
    await EnsureSchemaAsync(db);
    await PatternixSeed.SeedAsync(db, parser);
}

static async Task EnsureSchemaAsync(PatternixDbContext db)
{
    var connection = db.Database.GetDbConnection();
    if (connection.State != System.Data.ConnectionState.Open)
    {
        await connection.OpenAsync();
    }

    await using (var command = connection.CreateCommand())
    {
        command.CommandText = "SELECT OBJECT_ID(N'[4D_table_Theories]', N'U')";
        var exists = await command.ExecuteScalarAsync();
        if (exists is not null && exists != DBNull.Value)
        {
            await ApplySchemaFixupsAsync(db);
            return;
        }
    }

    var schemaPath = Path.Combine(AppContext.BaseDirectory, "sql", "schema.sql");
    var schema = await File.ReadAllTextAsync(schemaPath);
    await db.Database.ExecuteSqlRawAsync(schema);
    await ApplySchemaFixupsAsync(db);
    return;
}

static async Task ApplySchemaFixupsAsync(PatternixDbContext db)
{
    var fixups = """
IF COL_LENGTH(N'[4D_table_Datasets]', N'SourceText') IS NOT NULL
BEGIN
    ALTER TABLE [4D_table_Datasets] ALTER COLUMN SourceText NVARCHAR(MAX) NULL;
END;

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_4D_table_DatasetRows_DatasetId_RowNo'
      AND object_id = OBJECT_ID(N'[4D_table_DatasetRows]')
)
BEGIN
    DROP INDEX UX_4D_table_DatasetRows_DatasetId_RowNo ON [4D_table_DatasetRows];
END;

IF COL_LENGTH(N'[4D_table_TheoryResults]', N'CoverageScore') IS NOT NULL
BEGIN
    ALTER TABLE [4D_table_TheoryResults] ALTER COLUMN CoverageScore DECIMAL(18,4) NOT NULL;
    ALTER TABLE [4D_table_TheoryResults] ALTER COLUMN Confidence DECIMAL(18,4) NOT NULL;
    ALTER TABLE [4D_table_TheoryResults] ALTER COLUMN ForwardRate DECIMAL(18,4) NOT NULL;
    ALTER TABLE [4D_table_TheoryResults] ALTER COLUMN ReverseRate DECIMAL(18,4) NOT NULL;
END;

IF COL_LENGTH(N'[4D_table_CandidateSolutions]', N'Confidence') IS NOT NULL
BEGIN
    ALTER TABLE [4D_table_CandidateSolutions] ALTER COLUMN Confidence DECIMAL(18,4) NOT NULL;
END;
""";

    await db.Database.ExecuteSqlRawAsync(fixups);
}
