namespace cwiczenia7.Controllers;


using System;
using System.Data.SqlClient;
using cwiczenia7.Models;


public class WarehouseController
{
    private string connectionString = "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=apbd;Integrated Security=True;Connect Timeout=30;Encrypt=False;Trust Server Certificate=False;Application Intent=ReadWrite;Multi Subnet Failover=False";

    public int AddProductToWarehouse(WarehouseRequest request)
    {
        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            connection.Open();

            // czy produkt i magazyn istnieją
            if (!ProductExists(connection, request.IdProduct) || !WarehouseExists(connection, request.IdWarehouse))
                throw new Exception("Product or warehouse does not exist");

            // Spr czy ilość jest większa niż 0
            if (request.Amount <= 0)
                throw new Exception("Amount must be greater than 0");

            // Spr czy istnieje zamówienie na produkt
            int orderId = GetOrderId(connection, request.IdProduct, request.Amount, request.CreateAt);

            // Spr czy zamówienie nie zostało zrealizowane
            if (OrderFulfilled(connection, orderId))
                throw new Exception("Order has already been fulfilled");

            // Aktualizacja daty zrealizowania zamówienia
            UpdateOrderFulfilledAt(connection, orderId);

            // Wstawianie rekordow do tabeli Product_Warehouse
            int productWarehouseId = InsertProductWarehouse(connection, request);

            return productWarehouseId;
        }
    }

    private bool ProductExists(SqlConnection connection, int productId)
    {
        using (SqlCommand command = new SqlCommand("SELECT COUNT(*) FROM Product WHERE IdProduct = @ProductId", connection))
        {
            command.Parameters.AddWithValue("@ProductId", productId);
            return (int)command.ExecuteScalar() > 0;
        }
    }

    private bool WarehouseExists(SqlConnection connection, int warehouseId)
    {
        using (SqlCommand command = new SqlCommand("SELECT COUNT(*) FROM Warehouse WHERE IdWarehouse = @WarehouseId", connection))
        {
            command.Parameters.AddWithValue("@WarehouseId", warehouseId);
            return (int)command.ExecuteScalar() > 0;
        }
    }

    private int GetOrderId(SqlConnection connection, int productId, int amount, DateTime createdAt)
    {
        string query = @"SELECT TOP 1 IdOrder FROM [Order] 
                        WHERE IdProduct = @ProductId AND Amount = @Amount AND CreatedAt < @CreatedAt
                        ORDER BY CreatedAt DESC";

        using (SqlCommand command = new SqlCommand(query, connection))
        {
            command.Parameters.AddWithValue("@ProductId", productId);
            command.Parameters.AddWithValue("@Amount", amount);
            command.Parameters.AddWithValue("@CreatedAt", createdAt);

            object result = command.ExecuteScalar();
            if (result != null)
                return (int)result;
            else
                throw new Exception("Order not found");
        }
    }

    private bool OrderFulfilled(SqlConnection connection, int orderId)
    {
        using (SqlCommand command = new SqlCommand("SELECT COUNT(*) FROM Product_Warehouse WHERE IdOrder = @OrderId", connection))
        {
            command.Parameters.AddWithValue("@OrderId", orderId);
            return (int)command.ExecuteScalar() > 0;
        }
    }

    private void UpdateOrderFulfilledAt(SqlConnection connection, int orderId)
    {
        string query = "UPDATE [Order] SET FulfilledAt = GETDATE() WHERE IdOrder = @OrderId";
        using (SqlCommand command = new SqlCommand(query, connection))
        {
            command.Parameters.AddWithValue("@OrderId", orderId);
            command.ExecuteNonQuery();
        }
    }

    private int InsertProductWarehouse(SqlConnection connection, WarehouseRequest request)
    {
        string query = @"INSERT INTO Product_Warehouse (IdWarehouse, IdProduct, IdOrder, Amount, Price, CreatedAt)
                        VALUES (@IdWarehouse, @IdProduct, @IdOrder, @Amount, @Price, @CreatedAt);
                        SELECT SCOPE_IDENTITY();";

        using (SqlCommand command = new SqlCommand(query, connection))
        {
            command.Parameters.AddWithValue("@IdWarehouse", request.IdWarehouse);
            command.Parameters.AddWithValue("@IdProduct", request.IdProduct);
            command.Parameters.AddWithValue("@IdOrder", GetOrderId(connection, request.IdProduct, request.Amount, request.CreateAt));
            command.Parameters.AddWithValue("@Amount", request.Amount);
            command.Parameters.AddWithValue("@Price", GetProductPrice(connection, request.IdProduct) * request.Amount);
            command.Parameters.AddWithValue("@CreatedAt", DateTime.Now);

            return Convert.ToInt32(command.ExecuteScalar());
        }
    }

    private decimal GetProductPrice(SqlConnection connection, int productId)
    {
        using (SqlCommand command = new SqlCommand("SELECT Price FROM Product WHERE IdProduct = @ProductId", connection))
        {
            command.Parameters.AddWithValue("@ProductId", productId);
            return (decimal)command.ExecuteScalar();
        }
    }
}