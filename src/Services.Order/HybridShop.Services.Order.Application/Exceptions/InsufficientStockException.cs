namespace HybridShop.Services.Order.Application.Exceptions;

public class InsufficientStockException : Exception
{
    public InsufficientStockException(int requested, int available) 
        : base($"Nie można dodać {requested} szt. Dostępna ilość w magazynie to: {available}.")
    {
    }
}