namespace Server_PaymentForm_.NET.Models
{
    public class PaymentRequest
    {
        public double Amount { get; set; }
        public string? Currency { get; set; }
        public string? Email { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? IdentityType { get; set; }
        public string? IdentityCode { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }
        public string? Country { get; set; }
        public string? State { get; set; }
        public string? City { get; set; }
        public string? ZipCode { get; set; }
        public string? OrderId { get; set; }
    }

    public class PaymentResponse
    {
        public string? Status { get; set; }
        public AnswerData Answer { get; set; }
    }

    public class AnswerData
    {
        public string? FormToken { get; set; }
    }

    public class ResultModel
    {
        public string OrderStatus { get; set; } = string.Empty;
        public List<Transaction> Transactions { get; set; } = new List<Transaction>();
        public OrderDetails OrderDetails { get; set; } = new OrderDetails();
        public string KrHash { get; set; } = string.Empty;
        public string KrHashAlgorithm { get; set; } = string.Empty;
        public string KrAnswerType { get; set; } = string.Empty;
        public object Data { get; set; }
        public string KrHashKey { get; set; } = string.Empty;
        public object Pjson { get; set; }
    }

    public class Transaction
    {
        public string Currency { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    public class OrderDetails
    {
        public string OrderId { get; set; } = string.Empty;
    }
}
