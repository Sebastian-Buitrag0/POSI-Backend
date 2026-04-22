namespace POSI.Domain.DTOs.Gastrobar;

public record TableDto(
    Guid Id,
    string Name,
    int Capacity,
    string Status,
    bool IsActive,
    int ActiveOrderItemCount
);

public record CreateTableDto(string Name, int Capacity);
public record UpdateTableStatusDto(string Status);
