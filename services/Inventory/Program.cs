using System.Data;
using System.Reflection;
using Microsoft.Data.SqlClient;
using Shared.Persistence;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    var origin = builder.Configuration["StorefrontOrigin"] ?? "http://localhost:4200";
    if (origin == "*") policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    else policy.WithOrigins(origin).AllowAnyHeader().AllowAnyMethod();
}));

var connectionString = builder.Configuration.GetConnectionString("InventoryDb")
    ?? throw new InvalidOperationException("ConnectionStrings:InventoryDb is required.");
await SqlMigrations.ApplyAsync(connectionString, Assembly.GetExecutingAssembly(),
    builder.Configuration.GetValue("DatabaseAutoCreate", false));

var app = builder.Build();
app.UseCors();
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "inventory" }));
app.MapGet("/api/inventory/{productId:int}", async (int productId, CancellationToken ct) =>
{
    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync(ct);
    await using var command = new SqlCommand(
        "SELECT OnHand - Reserved FROM dbo.StockItems WHERE ProductId = @productId;", connection);
    command.Parameters.Add("@productId", SqlDbType.Int).Value = productId;
    var result = await command.ExecuteScalarAsync(ct);
    return Results.Ok(new { productId, available = result is null or DBNull ? 0 : Convert.ToInt32(result) });
});
app.MapPost("/api/inventory/reservations", async (ReservationRequest request, CancellationToken ct) =>
{
    if (request.Items is null || request.Items.Count == 0 ||
        request.Items.Any(item => item.ProductId <= 0 || item.Quantity <= 0) ||
        request.Items.GroupBy(item => item.ProductId).Any(group => group.Count() > 1))
        return Results.BadRequest(new { error = "Provide at least one unique valid item." });

    var reservationId = Guid.NewGuid();
    var orderId = request.OrderId.GetValueOrDefault(Guid.NewGuid());
    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync(ct);
    await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(ct);

    foreach (var item in request.Items)
    {
        await using var reserve = new SqlCommand(
            """
            UPDATE dbo.StockItems
            SET Reserved = Reserved + @quantity, UpdatedAt = SYSUTCDATETIME()
            WHERE ProductId = @productId AND OnHand - Reserved >= @quantity;
            """, connection, transaction);
        reserve.Parameters.Add("@productId", SqlDbType.Int).Value = item.ProductId;
        reserve.Parameters.Add("@quantity", SqlDbType.Int).Value = item.Quantity;
        if (await reserve.ExecuteNonQueryAsync(ct) != 1)
        {
            await transaction.RollbackAsync(ct);
            return Results.Conflict(new { error = "Insufficient inventory." });
        }

        await using var record = new SqlCommand(
            """
            INSERT dbo.InventoryReservations(ReservationId, OrderId, ProductId, Quantity, Status)
            VALUES (@reservationId, @orderId, @productId, @quantity, 'reserved');
            """, connection, transaction);
        record.Parameters.Add("@reservationId", SqlDbType.UniqueIdentifier).Value = reservationId;
        record.Parameters.Add("@orderId", SqlDbType.UniqueIdentifier).Value = orderId;
        record.Parameters.Add("@productId", SqlDbType.Int).Value = item.ProductId;
        record.Parameters.Add("@quantity", SqlDbType.Int).Value = item.Quantity;
        await record.ExecuteNonQueryAsync(ct);
    }

    await transaction.CommitAsync(ct);
    return Results.Ok(new { reservationId, status = "reserved" });
});
app.MapPost("/api/inventory/release", async (ReleaseRequest request, CancellationToken ct) =>
{
    if (request.ReservationId == Guid.Empty)
        return Results.BadRequest(new { error = "A reservation ID is required." });

    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync(ct);
    await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(ct);
    var items = new List<InventoryItem>();
    await using (var read = new SqlCommand(
        """
        SELECT ProductId, Quantity
        FROM dbo.InventoryReservations WITH (UPDLOCK, HOLDLOCK)
        WHERE ReservationId = @reservationId AND Status = 'reserved';
        """, connection, transaction))
    {
        read.Parameters.Add("@reservationId", SqlDbType.UniqueIdentifier).Value = request.ReservationId;
        await using var reader = await read.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            items.Add(new InventoryItem(reader.GetInt32(0), reader.GetInt32(1)));
    }
    if (items.Count == 0)
    {
        await using var alreadyReleased = new SqlCommand(
            "SELECT COUNT(*) FROM dbo.InventoryReservations WHERE ReservationId = @reservationId AND Status = 'released';",
            connection, transaction);
        alreadyReleased.Parameters.Add("@reservationId", SqlDbType.UniqueIdentifier).Value = request.ReservationId;
        var wasReleased = Convert.ToInt32(await alreadyReleased.ExecuteScalarAsync(ct)) > 0;
        await transaction.RollbackAsync(ct);
        if (wasReleased)
            return Results.Ok(new { reservationId = request.ReservationId, status = "released" });
        return Results.NotFound(new { error = "Active reservation not found." });
    }

    foreach (var item in items)
    {
        await using var release = new SqlCommand(
            """
            UPDATE dbo.StockItems
            SET Reserved = Reserved - @quantity, UpdatedAt = SYSUTCDATETIME()
            WHERE ProductId = @productId AND Reserved >= @quantity;
            """, connection, transaction);
        release.Parameters.Add("@productId", SqlDbType.Int).Value = item.ProductId;
        release.Parameters.Add("@quantity", SqlDbType.Int).Value = item.Quantity;
        if (await release.ExecuteNonQueryAsync(ct) != 1)
        {
            await transaction.RollbackAsync(ct);
            return Results.Problem("Inventory reservation state is inconsistent.", statusCode: 500);
        }
    }

    await using (var update = new SqlCommand(
        "UPDATE dbo.InventoryReservations SET Status = 'released' WHERE ReservationId = @reservationId AND Status = 'reserved';",
        connection, transaction))
    {
        update.Parameters.Add("@reservationId", SqlDbType.UniqueIdentifier).Value = request.ReservationId;
        await update.ExecuteNonQueryAsync(ct);
    }
    await transaction.CommitAsync(ct);
    return Results.Ok(new { reservationId = request.ReservationId, status = "released" });
});
app.Run();

public sealed record InventoryItem(int ProductId, int Quantity);
public sealed record ReservationRequest(IReadOnlyList<InventoryItem> Items, Guid? OrderId = null);
public sealed record ReleaseRequest(Guid ReservationId);
